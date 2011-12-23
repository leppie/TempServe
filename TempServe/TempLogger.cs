using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlServerCe;
using System.Threading;
using System.IO;
using System.Diagnostics;
using System.Data;
using System.Runtime.InteropServices;

namespace TempServe
{
  public enum SampleType
  {
    Temperature,
    FanSpeed,
    Voltage,
    Other
  }

  [Serializable]
  public class Sample
  {
    public SampleType Type { get; private set; }
    public string Name { get; private set; }
    public double Value { get; private set; }
    public bool Log { get; private set; }
    public DateTime Time { get; set; }

    public string FormattedValue
    {
      get
      {
        return Value.ToString(this.GetFormat()); 
      }
    }

    public static Sample Temperature(string name, double value, bool log)
    {
      return new Sample
      {
        Type = SampleType.Temperature,
        Name = name,
        Value = value,
        Log = log
      };
    }

    public static Sample Voltage(string name, double value, bool log)
    {
      return new Sample
      {
        Type = SampleType.Voltage,
        Name = name,
        Value = value,
        Log = log
      };
    }

    public static Sample FanSpeed(string name, double value, bool log)
    {
      return new Sample
      {
        Type = SampleType.FanSpeed,
        Name = name,
        Value = value,
        Log = log
      };
    }

    public static Sample Other(string name, double value, bool log)
    {
      return new Sample
      {
        Type = SampleType.Other,
        Name = name,
        Value = value,
        Log = log
      };
    }
  }

  public static class SampleExtensions
  {
    public static string GetFormat(this Sample s)
    {
      switch (s.Type)
      {
        case SampleType.Temperature:
          return "F1";
        case SampleType.FanSpeed:
          return "F0";
        case SampleType.Voltage:
          return "F3";
        case SampleType.Other:
        default:
          return "F2";
      }
    }
  }

  [Serializable]
  public class FanSettings
  {
    public string Name { get; set; }

    public bool AutoSpeed { get; set; }

    public int TempLow { get; set; }
    public int TempHigh { get; set; }
    public int SpeedLow { get; set; }
    public int SpeedHigh { get; set; }

    public int SpeedManual { get; set; }

  }

  public interface IFanController
  {
    IEnumerable<FanSettings> GetSettings();
    void UpdateSettings(FanSettings fs);
  }

  class TempLogger : MarshalByRefObject, IData, IFanController
  {
    static Dictionary<string, Sample> HashMap(Func<Sample, Sample, Sample> sel,
      Dictionary<string, Sample> l1, Dictionary<string, Sample> l2)
    {
      var res = new Dictionary<string, Sample>();
      foreach (var kvp in l1)
      {
        var v2 = l2[kvp.Key];
        res[kvp.Key] = sel(kvp.Value, v2);
      }
      return res;
    }

    static void RecordSample(SqlCeResultSet rs, IEnumerable<Sample> data)
    {
      var rec = rs.CreateRecord();

      foreach (var smp in data)
      {
        if (smp.Log)
        {
          rec[smp.Name] = smp.Value;
        }
      }

      rs.Insert(rec);
    }

        // Declare the SetConsoleCtrlHandler function
    // as external and receiving a delegate.

    [DllImport("kernel32")]
    static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);

    // A delegate type to be used as the handler routine
    // for SetConsoleCtrlHandler.
    delegate bool HandlerRoutine(CtrlTypes CtrlType);

    // An enumerated type for the control messages
    // sent to the handler routine.
    enum CtrlTypes
    {
        CTRL_C_EVENT = 0,
        CTRL_BREAK_EVENT,
        CTRL_CLOSE_EVENT,
        CTRL_LOGOFF_EVENT = 5,
        CTRL_SHUTDOWN_EVENT
    }

    static bool ConsoleCtrlCheck(CtrlTypes ctrlType)
    {
        // Put your own handler here
        return true;
    }

    internal volatile static bool running = true;

    static IEnumerable<Sample> lastsample;
    static IFanController fc;

    internal static void StartTempLogging(bool consolemode, bool logmode, bool dumpmode)
    {
      bool initialized = false;

      if (consolemode)
      {
        Console.Clear();
        Console.Title = "TempServe";
        Console.BufferHeight = Console.WindowHeight = 20;
        Console.BufferWidth = Console.WindowWidth = 80;
        Console.CursorVisible = false;
        Console.WriteLine("Initializing ...");
      }

      SqlCeResultSet rs = null;

      using (var mon = new W83627DHG())
      {
        fc = mon;
        var cpu = new CPU();
        var asp0800 = new SMBus.ASP0800();
        var ard = new Arduino();
#if NVIDIA
        Debugger.Launch();
        var nv = new NVidiaGPU();
#endif

        if (consolemode)
        {
          SetConsoleCtrlHandler(
            x =>
            {
              running = false;
              mon.Dispose();
              Console.CursorVisible = true;
              return x != CtrlTypes.CTRL_C_EVENT;
            }
            , true);
        }

        Dictionary<string, Sample> min = null;
        Dictionary<string, Sample> max = null;

        var avg5 = new Queue<Dictionary<string, Sample>>(60);

        var sw = Stopwatch.StartNew();

        while (running)
        {
          var elapsed = sw.Elapsed;

          var alldata = cpu.GetData().
            Concat(mon.GetData()).
            Concat(asp0800.GetData()).
            Concat(ard.GetData()).
#if NVIDIA
            Concat(nv.GetData()).
#endif
            ToList();
          
          var now = DateTime.Now;

          foreach (var smp in alldata)
          {
            smp.Time = now;
          }

          lastsample = alldata;

          if (!initialized)
          {
            if (consolemode)
            {
              var height = alldata.Count + 2;
              Console.BufferHeight = Console.WindowHeight = height;
            }

            if (logmode)
            {
              rs = BuildDatabase(alldata.Where(x => x.Log).ToList());
            }

            initialized = true;
          }

          if (logmode)
          {
            RecordSample(rs, alldata);
          }

          if (dumpmode)
          {
            using (var w = new StringWriter())
            {
              mon.DumpRawByteData(w);
              File.AppendAllText("W83627DHG.log", w.ToString());
            }

            using (var w = new StringWriter())
            {
              mon.DumpSIOByteData(w);
              File.AppendAllText("Super-IO.log", w.ToString());
            }

            //using (var w = new StringWriter())
            //{
            //  asp0800.DumpRawByteData(w);
            //  File.AppendAllText("ASP0800b.log", w.ToString());
            //}

            using (var w = new StringWriter())
            {
              asp0800.DumpRawWordData(w);
              File.AppendAllText("ASP0800w.log", w.ToString());
            }
          }

          if (consolemode)
          {
            Console.Title = string.Format("TempServe - Running for: {3}d {0:00}:{1:00}:{2:00}",
              elapsed.Hours, elapsed.Minutes, elapsed.Seconds, elapsed.Days);

            while (avg5.Count >= 60)
            {
              avg5.Dequeue();
            }

            var hashdata = alldata.ToDictionary(x => x.Name, x => x); ;

            avg5.Enqueue(hashdata);

            if (min == null)
            {
              min = hashdata;
            }
            else
            {
              min = HashMap((x, y) => x.Value > y.Value ? y : x, min, hashdata);
            }

            if (max == null)
            {
              max = hashdata;
            }
            else
            {
              max = HashMap((x, y) => x.Value < y.Value ? y : x, max, hashdata);
            }

            using (var output = new StringWriter())
            {
              output.WriteLine("{0,-18}{1,10}{2,10}{3,10}{4,10}{5,10}{6,10}",
                "Sensor", "Current", "Min", "Max", "Avg(10)", "Avg(30)", "Avg(60)");

              foreach (var s in alldata)
              {
                var avg = avg5.Reverse().ToArray();

                var fs = BuildFormatString(s);

                output.WriteLine(fs, s.Name, s.Value,
                  min[s.Name].Value,
                  max[s.Name].Value,
                  avg.Take(10).Select(x => x[s.Name]).Average(x => x.Value),
                  avg.Take(30).Select(x => x[s.Name]).Average(x => x.Value),
                  avg.Take(60).Select(x => x[s.Name]).Average(x => x.Value)
                  );
              }

              Console.Clear();
              Console.Write(output.ToString());
            }
          }

          var taken = Math.Min((sw.Elapsed - elapsed).TotalMilliseconds, 100) + 3;
          Thread.Sleep(1000 - (int)taken);
        }
      }
    }

    static string BuildFormatString(Sample s)
    {
      var fs = s.GetFormat();
      return "{0,-18}{1,10:F2}{2,10:F2}{3,10:F2}{4,10:F2}{5,10:F2}{6,10:F2}".Replace("F2", fs);
    }

    static SqlCeResultSet BuildDatabase(List<Sample> alldata)
    {
      var cs = "Data Source=Log.sdf;Max Database Size=4091";
      var con = new SqlCeConnection(cs);
      

      Environment.CurrentDirectory = Path.GetDirectoryName(typeof(TempLogger).Assembly.Location);
      if (File.Exists("Log.sdf"))
      {
        //see if schema is the same
        con.Open();

        var chkschema = con.CreateCommand();

        chkschema.CommandText = @"
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Sample' AND COLUMN_NAME <> 'Time'";

        var currentcols = new HashSet<string>();

        using (var reader = chkschema.ExecuteReader())
        {
          while (reader.Read())
          {
            currentcols.Add(reader.GetString(0));
          }
        }

        if (currentcols.Count != alldata.Count || CompareColumns(currentcols, alldata))
        {
          con.Close();
          CreateNewDataBase(cs, con, alldata);
        }
      }
      else
      {
        CreateNewDataBase(cs, con, alldata);
      }

      var cmd = con.CreateCommand();
      cmd.CommandType = System.Data.CommandType.TableDirect;
      cmd.CommandText = "Sample";

      return cmd.ExecuteResultSet(ResultSetOptions.Updatable);
    }

    static bool CompareColumns(HashSet<string> currentcols, List<Sample> alldata)
    {
      foreach (var kvp in alldata)
      {
        if (!currentcols.Contains(kvp.Name))
        {
          return true;
        }
      }

      return false;
    }

    static void CreateNewDataBase(string cs, SqlCeConnection con, List<Sample> alldata)
    {
      if (File.Exists("Log.sdf"))
      {
        File.Move("Log.sdf", string.Format("Log-{0:yyyyMMdd-HHmmss}.sdf", DateTime.Now));
      }

      var ce = new SqlCeEngine(cs);

      ce.CreateDatabase();

      con.Open();

      var buildtable = con.CreateCommand();
      buildtable.CommandText = string.Format(
@"CREATE TABLE [Sample] ([Time] datetime NOT NULL DEFAULT getdate(), {0});",
string.Join(", ", alldata.Select(x => string.Format("[{0}] float NOT NULL", x.Name)).ToArray()));

      buildtable.ExecuteNonQuery();

      buildtable.CommandText = @"ALTER TABLE [Sample] ADD PRIMARY KEY ([Time]);";

      buildtable.ExecuteNonQuery();
    }

    public IEnumerable<Sample> GetData()
    {
      return lastsample;
    }

    public IEnumerable<FanSettings> GetSettings()
    {
      if (fc == null)
      {
        return null;
      }
      return fc.GetSettings();
    }

    public void UpdateSettings(FanSettings fs)
    {
      if (fc != null)
      {
        fc.UpdateSettings(fs);
      }
    }
  }
}
