using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.Remoting;
using TempServe;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Windows.Forms.DataVisualization.Charting;
using System.Data.SqlServerCe;

namespace TempServe
{
  public partial class TempServeUI : Form
  {
    IData service;
    IFanController fancontroller;

    Dictionary<string, Sample> min = null;
    Dictionary<string, Sample> max = null;

    Queue<Dictionary<string, Sample>> avg5 = new Queue<Dictionary<string, Sample>>(300);

    Dictionary<SampleType, string> chartmap = new Dictionary<SampleType, string>();
    Dictionary<SampleType, string> legendmap = new Dictionary<SampleType, string>();

    static Dictionary<string, Sample> HashMap(Func<Sample, Sample, Sample> sel,
        Dictionary<string, Sample> l1, Dictionary<string, Sample> l2)
    {
      var res = new Dictionary<string, Sample>(l1.Count);
      foreach (var kvp in l1)
      {
        var v2 = l2[kvp.Key];
        res[kvp.Key] = sel(kvp.Value, v2);
      }
      return res;
    }

    public TempServeUI()
    {
      InitializeComponent();

      InitChart();
      InitRemoting();
      LoadPersistedData();
    }

    protected override void OnLoad(EventArgs e)
    {
      base.OnLoad(e);

      timer1_Tick(null, EventArgs.Empty);
      timer1_Tick(null, EventArgs.Empty);

      timer1.Enabled = true;
    }

    void LoadPersistedData()
    {
      var cs = "Data Source=Log.sdf;Max Database Size=4091";
      using (var con = new SqlCeConnection(cs))
      {

        var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT * FROM Sample WHERE [Time] > @time";
        cmd.Parameters.Add("@time", DateTime.Now.AddMinutes(-5));

        con.Open();

        using (var dr = cmd.ExecuteReader())
        {
          while (dr.Read())
          {
            var dic = new Dictionary<string, Sample>();

            var time = dr.GetDateTime(0);

            for (int i = 1; i < dr.FieldCount; i++)
            {
              var name = dr.GetName(i);
              var value = dr.GetDouble(i);

              // :(
              var s = Sample.Other(name, value, false);
              s.Time = time;
              dic[name] = s;
            }

            double[] temps = { dic["CORE 1"].Value, dic["CORE 2"].Value, dic["CORE 3"].Value, dic["CORE 4"].Value };

            var cpuavg = Sample.Temperature("CORE AVG", temps.Average(), false);
            cpuavg.Time = time;

            dic["CORE AVG"] = cpuavg;

            var cpumax = Sample.Temperature("CORE MAX", temps.Max(), false);
            cpumax.Time = time;

            dic["CORE MAX"] = cpumax;

            avg5.Enqueue(dic);
          }
        }

        con.Close();
      }
    }

    void InitRemoting()
    {
      BinaryClientFormatterSinkProvider sbs = new BinaryClientFormatterSinkProvider();

      IpcClientChannel client = new IpcClientChannel("TempServe", sbs);
      client.IsSecured = false;

      ChannelServices.RegisterChannel(client, false);

      service = RemotingServices.Connect(typeof(IData), "ipc://TempServe/DataService") as IData;
      fancontroller = RemotingServices.Connect(typeof(IFanController), "ipc://TempServe/DataService") as IFanController;
    }

    void InitChart()
    {
      chartmap[SampleType.Temperature] = "tempChart";
      chartmap[SampleType.FanSpeed] = "fanChart";
      chartmap[SampleType.Voltage] = "voltChart";
      chartmap[SampleType.Other] = "voltChart";

      legendmap[SampleType.Temperature] = "tempLegend";
      legendmap[SampleType.FanSpeed] = "fanLegend";
      legendmap[SampleType.Voltage] = "voltLegend";
      legendmap[SampleType.Other] = "voltLegend";

      chart.Series.Clear();
    }

    Dictionary<string, SampleWrap> mapping;

    void timer1_Tick(object sender, EventArgs e)
    {
      var data = service.GetData();
      
      if (data == null)
      {
        return;
      }

      var alldata = data.ToList();

      while (avg5.Count >= 300)
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

      if (updatefansettings)
      {
        fanSettingsBindingSource.DataSource = fancontroller.GetSettings();
        updatefansettings = false;
      }

      if (!(sampleWrapBindingSource.DataSource is List<SampleWrap>))
      {
        var bs = TransformData(alldata).ToList();
        mapping = bs.ToDictionary(x => x.Name);

        sampleWrapBindingSource.DataSource = bs;

        EnableDefaultGraphs();
      }
      else
      {
        var avg = avg5.Reverse().ToArray();

        foreach (var s in alldata)
        {
          var format = s.GetFormat();

          var sw = mapping[s.Name];

          sw.Current = s.Value.ToString(format);
          sw.Min = min[s.Name].Value.ToString(format);
          sw.Max = max[s.Name].Value.ToString(format);

          var avg10 = avg.Take(10).Where(x => x.ContainsKey(s.Name)).Select(x => x[s.Name]);
          var avg30 = avg.Take(30).Where(x => x.ContainsKey(s.Name)).Select(x => x[s.Name]);
          var avg60 = avg.Take(60).Where(x => x.ContainsKey(s.Name)).Select(x => x[s.Name]);

          sw.Avg10 = avg10.Average(x => x.Value).ToString(format);
          sw.Avg30 = avg30.Average(x => x.Value).ToString(format);
          sw.Avg60 = avg60.Average(x => x.Value).ToString(format);

          var chartarea = chartmap[s.Type];

          if (sw.ShowOnGraph)
          {
            Series sr;
            if (!series.TryGetValue(sw.Name, out sr))
            {
              sr = series[sw.Name] = chart.Series.Add(sw.Name);
              sr.ChartArea = chartarea;
              sr.ChartType = SeriesChartType.FastLine;
              sr.Font = Font;
              sr.ToolTip =
@"#SERIESNAME
Value: #VAL{N3}
Avg: #AVG{N3}
Max: #MAX{N3}
Min: #MIN{N3}";

              sr.Legend = legendmap[s.Type];
            }

            sr.Points.DataBind(avg5.Where(x => x.ContainsKey(s.Name)).Select(x => x[s.Name]).ToList(), "Time", "Value", "");
          }
          else
          {
            Series sr;
            if (series.TryGetValue(sw.Name, out sr))
            {
              chart.Series.Remove(sr);
              series.Remove(sw.Name);
            }
          }
        }

        chart.Invalidate();
        dataGridView1.Invalidate();
      }
    }

    void EnableDefaultGraphs()
    {
      mapping["CPUTIN"].ShowOnGraph = true;
      mapping["CORE MAX"].ShowOnGraph = true;
      mapping["CORE AVG"].ShowOnGraph = true;
      mapping["Motherboard"].ShowOnGraph = true;

      mapping["CPUFAN0"].ShowOnGraph = true;
      mapping["CPUFAN1"].ShowOnGraph = true;
      mapping["SYSFAN"].ShowOnGraph = true;
      mapping["AUXFAN"].ShowOnGraph = true;

      mapping["Multiplier"].ShowOnGraph = true;
      mapping["CPU Load"].ShowOnGraph = true;
    }

    Dictionary<string, Series> series = new Dictionary<string, Series>();

    IEnumerable<SampleWrap> TransformData(IEnumerable<Sample> data)
    {
      var avg = avg5.Reverse().ToArray();

      foreach (var s in data)
      {
        var format = s.GetFormat();

        yield return new SampleWrap
        {
          Name = s.Name,
          Current = s.Value.ToString(format),
          Min = min[s.Name].Value.ToString(format),
          Max = max[s.Name].Value.ToString(format),
          Avg10 = avg.Take(10).Where(x => x.ContainsKey(s.Name)).Select(x => x[s.Name]).Average(x => x.Value).ToString(format),
          Avg30 = avg.Take(30).Where(x => x.ContainsKey(s.Name)).Select(x => x[s.Name]).Average(x => x.Value).ToString(format),
          Avg60 = avg.Take(60).Where(x => x.ContainsKey(s.Name)).Select(x => x[s.Name]).Average(x => x.Value).ToString(format),
        };
      }
    }

    void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
    {
      if (e.ColumnIndex == 7 && e.RowIndex >= 0)
      {
        var sw = dataGridView1.Rows[e.RowIndex].DataBoundItem as SampleWrap;
        sw.ShowOnGraph = !sw.ShowOnGraph;
      }
    }

    void toolStripButton3_CheckedChanged(object sender, EventArgs e)
    {
      chart.ChartAreas[2].Visible = toolStripButton3.Checked;
      UpdateChartVisibility();
    }

    void toolStripButton2_CheckedChanged(object sender, EventArgs e)
    {
      chart.ChartAreas[1].Visible = toolStripButton2.Checked;
      UpdateChartVisibility();
    }

    void toolStripButton1_CheckedChanged(object sender, EventArgs e)
    {
      chart.ChartAreas[0].Visible = toolStripButton1.Checked;
      UpdateChartVisibility();
    }

    void UpdateChartVisibility()
    {
      ClientSize = new System.Drawing.Size(chart.ChartAreas.Any(x => x.Visible) ? 1264 : 411, 637); 
    }

    bool updatefansettings = true;

    void dataGridView2_CellContentClick(object sender, DataGridViewCellEventArgs e)
    {
      if (e.RowIndex >= 0 && e.ColumnIndex == 7)
      {
        var fs = dataGridView2.Rows[e.RowIndex].DataBoundItem as FanSettings;
        fancontroller.UpdateSettings(fs);

        updatefansettings = true;
      }
    }
  }

  class SampleWrap
  {
    public string Name { get; set; }

    public string Current { get; set; }

    public string Min { get; set; }
    public string Max { get; set; }

    public string Avg10 { get; set; }
    public string Avg30 { get; set; }
    public string Avg60 { get; set; }

    public bool ShowOnGraph {get;set;}
  }
}
