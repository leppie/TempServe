using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using OpenLibSys;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Management;
using System.IO;
using System.IO.Ports;
#if NVIDIA
using OpenHardwareMonitor.Hardware.Nvidia;
#endif

namespace TempServe
{
  class HardwareAccess
  {
    protected readonly static Ols Ols;

    static HardwareAccess()
    {
      Ols = new Ols();
    }
  }

  class IndexedIOPort : HardwareAccess
  {
    public int BaseAddress { get; set; }
    public int AddressPortOffset { get; set; }
    public int DataPortOffset { get; set; }

    public int AddressPort { get { return BaseAddress + AddressPortOffset; } }
    public int DataPort { get { return BaseAddress + DataPortOffset; } }

    public int BankSelect { get; set; }

    public void Write(int index, int value)
    {
      Ols.WriteIoPortByte((ushort)AddressPort, (byte)index);
      Ols.WriteIoPortByte((ushort)DataPort, (byte)value);
    }

    public int Read(int index)
    {
      Ols.WriteIoPortByte((ushort)AddressPort, (byte)index);
      var d = Ols.ReadIoPortByte((ushort)DataPort);
      return d;
    }

    public virtual void Enter()
    {

    }

    public virtual void Exit()
    {

    }

    public void Select(int index)
    {
      Write(BankSelect, index);
    }

  }

  class Arduino : IData
  {
    bool available = false;
    SerialPort port;
    double water, air, room, outside;

    public Arduino()
    {
      port = new SerialPort("COM6", 115200);
      port.DataReceived +=new SerialDataReceivedEventHandler(port_DataReceived);

      try
      {
        port.Open();
        available = true;
      }
      catch (Exception ex)
      {
        throw ex;
      }
    }

    void  port_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
 	    if (e.EventType == SerialData.Chars)
      {
        string line = port.ReadLine();
        if (!line.StartsWith(";"))
        {
          var tokens = line.Split(',');

          water = Convert.ToDouble(tokens[0]);
          air = Convert.ToDouble(tokens[1]);
          room = Convert.ToDouble(tokens[2]);
          outside = Convert.ToDouble(tokens[3]);
        }
      }
    }

    public IEnumerable<Sample> GetData()
    {
      if (available)
      {
        yield return Sample.Temperature("Water", water, true);
        yield return Sample.Temperature("Air", air, true);
        yield return Sample.Temperature("Room", room, true);
        yield return Sample.Temperature("Outside", outside, true);
      }
    }
  }

  // from various google code source and some experimentation
  class SMBus : HardwareAccess
  {
    public ushort BaseAddress = 0;

    const ushort STATUS = 0x0;
    const ushort CONTROL = 0x2;
    const ushort COMMAND = 0x3;
    const ushort SLAVE_TX = 0x4;
    const ushort DATA0 = 0x5;
    const ushort DATA1 = 0x6;
    const ushort BLOCKDATA = 0x7;
    const ushort SLAVE_RX = 0x9;
    const ushort SLAVE_DATA = 0xA;

    const int WRITE = 0;
    const int READ = 1;

    ushort Address(ushort x)
    {
      return (ushort)(BaseAddress + x);
    }

    bool Available
    {
      get { return BaseAddress != 0; }
    }

    public SMBus()
    {
      var dev = Ols.FindPciDeviceById(0x8086, 0x3b30, 0);

      if (dev != 0xffffffff)
      {
        var msb = Ols.ReadPciConfigByte(dev, 0x80);
        var lsb = Ols.ReadPciConfigByte(dev, 0x81);

        BaseAddress = (ushort)((msb << 8) | lsb);
      }
    }

    void WaitForFree()
    {
      var status = Ols.ReadIoPortByte(BaseAddress);

      while ((status & 1) != 0)
      {
        Thread.Sleep(1);
        status = Ols.ReadIoPortByte(BaseAddress);
      }
      if ((status & 0x1e) != 0)
      {
        Ols.WriteIoPortByte(BaseAddress, status);
      }
    }

    void WaitForEnd()
    {
      Thread.Sleep(1);
      var status = Ols.ReadIoPortByte(BaseAddress);

      while ((status & 1) == 1)
      {
        Thread.Sleep(1);
        status = Ols.ReadIoPortByte(BaseAddress);
      }
    }

    public int Call(int slaveaddress, int cmd, int rw)
    {
      return Call(slaveaddress, cmd, rw, 1);
    }

    public int Call(int slaveaddress, int cmd, int rw, int length)
    {
      WaitForFree();

      Ols.WriteIoPortByte(Address(COMMAND), (byte)cmd);
      Ols.WriteIoPortByte(Address(SLAVE_TX), (byte)((slaveaddress << 1) | rw));
      Ols.WriteIoPortByte(Address(CONTROL), (byte)(length == 1 ? 0x48 : 0x4c));

      Thread.Sleep(1);
      WaitForEnd();

      var lsb = Ols.ReadIoPortByte(Address(DATA0));
      var msb = Ols.ReadIoPortByte(Address(DATA1));
      var r = lsb | (msb << 8);

      return r;
    }

    public void SetReg(int slaveaddress, int reg, int data)
    {
      WaitForFree();
      Ols.WriteIoPortWord(Address(DATA0), (ushort)data);
      Ols.WriteIoPortWord(Address(DATA1), (ushort)data);
      Call(slaveaddress, reg, WRITE);
    }

    public int GetReg(int slaveaddress, int reg)
    {
      return GetReg(slaveaddress, reg, 1) & 0xff;
    }

    public int GetReg(int slaveaddress, int reg, int length)
    {
      WaitForFree();
      Ols.WriteIoPortWord(Address(DATA0), 0);
      Ols.WriteIoPortWord(Address(DATA1), 0);
      var r = Call(slaveaddress, reg, READ, length);
      return r & 0xffff;
    }

    // http://www.datasheetdir.com/ASP0800+DC-DC-Controllers
    // http://pmbus.org/docs/PMBus_Specification_Part_II_Rev_1_0_20050328.pdf
    public class ASP0800 : IData
    {
      SMBus smbus;
      const int ADDRESS = 0x20;

      public ASP0800()
      {
        smbus = new SMBus();
      }
      
      public ASP0800(SMBus smbus)
      {
        this.smbus = smbus;
      }

      bool check = true;
      bool available = false;

      public IEnumerable<Sample> GetData()
      {
        if (check)
        {
          check = false;

          if (smbus.Available)
          {
            int MFR_MODEL = smbus.GetReg(ADDRESS, 0x9a, 2);
            available = MFR_MODEL == 0x3298;
          }
          else
          {
            available = false;
          }
        }

        if (available)
        {
          int VOUT = smbus.GetReg(ADDRESS, 0x8b, 2);
          int IOUT = smbus.GetReg(ADDRESS, 0x8c, 2);
          int POUT = smbus.GetReg(ADDRESS, 0x96, 2);

          var VID = GetVID(VOUT);
          var Power = ReadLiteral(POUT);
          var Current = ReadLiteral(IOUT);

          yield return Sample.Voltage("CPU VID", VID, true);
          yield return Sample.Other("CPU Current", Current, true);
          yield return Sample.Other("CPU Power", Power, true);
        }
      }

      internal void DumpRawByteData(TextWriter writer)
      {
        writer.WriteLine(DateTime.Now);
        writer.Write("    ");

        for (int i = 0; i < 16; i++)
        {
          writer.Write("{0:X2} ", i);
        }

        writer.WriteLine();

        for (int i = 0; i < 16; i++)
        {
          writer.Write("{0:X2}  ", i * 16);

          for (int j = 0; j < 16; j++)
          {
            int reg = i * 16 + j;

            var r = smbus.GetReg(ADDRESS, reg);

            writer.Write("{0:X2} ", r);
          }
          writer.WriteLine();
        }
        writer.WriteLine();
      }

      internal void DumpRawWordData(TextWriter writer)
      {
        writer.WriteLine(DateTime.Now);
        writer.Write("    ");

        for (int i = 0; i < 16; i++)
        {
          writer.Write("{0:X4} ", i);
        }

        writer.WriteLine();

        for (int i = 0; i < 16; i++)
        {
          writer.Write("{0:X2}  ", i * 16);

          for (int j = 0; j < 16; j++)
          {
            int reg = i * 16 + j;

            var r = smbus.GetReg(ADDRESS, reg, 2);

            writer.Write("{0:X4} ", r);
          }
          writer.WriteLine();
        }

        writer.WriteLine();
      }
    }

    // TODO: add edge cases
    static double GetVID(int VOUT)
    {
      VOUT &= 0xFF;
      var VID = VOUT > 83 ? (1.08750 - (VOUT - 84) * 0.00625) : 1.6 - (VOUT - 2) * 0.00625;
      return VID;
    }

    static double ReadLiteral(int r)
    {
      var N = TwoComplementBits(r >> 11, 5);
      var X = TwoComplementBits(r & 0x7ff, 11);
      return X * Math.Pow(2, N);
    }

    // this one was hard, I was sick :*(
    static int TwoComplementBits(int x, int bits)
    {
      if (x >= (1 << (bits - 1)))
      {
        return x - (1 << (bits));
      }
      return x;
    }
  }

  /// <summary>
  /// Generic data sample interface
  /// </summary>
  public interface IData
  {
    IEnumerable<Sample> GetData();
  }

  // from intel specs and experimentation 
  class CPU : HardwareAccess, IData
  {
    static uint tjmax = 0;

    const int SAMPLE_RATE = 10;
    const int CORE_COUNT = 4;

    static uint[,] samples = new uint[SAMPLE_RATE, CORE_COUNT];

    static Thread reader, //rtcloop, 
      tscloop;

    static PerformanceCounter pc = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);

    // Monitors the average DST temp over a 1 second interval
    static void ReadLoop()
    {
      int r = 0, index = 0;
      uint eax = 0, edx = 0;

      // MSR_TEMPERATURE_TARGET
      // for PROCHOT assertion (probably same as TjMax), only works on i7
      r = Ols.Rdmsr(0x1a2, ref eax, ref edx);
      tjmax = (eax >> 16) & 0xFF;

      while (true)
      {
        for (int i = 0; i < CORE_COUNT; i++)
        {
          // for some reason you need to shift 2 bits on Win64
          UIntPtr mask = (UIntPtr)(1 << (i * 4/2));

          r = Ols.RdmsrPx(0x19c, ref eax, ref edx, mask);

          if (r != 0 && (eax & 0x80000000) != 0)
          {
            samples[index, i] = eax;
          }          
        }

        Thread.Sleep(1000 / SAMPLE_RATE);
        
        index++;
        if (index == SAMPLE_RATE)
        {
          index = 0;
        }
      }
    }

    // a thread for the Real Time Clock (RTS)
    static void RTCLoop()
    {
      uint eax = 0, edx = 0;

      while (true)
      {
        var tsm1 = timeGetTime();

        // IA32_TIME_STAMP_COUNTER
        var r = Ols.Rdtsc(ref eax, ref edx);

        var tsc = eax | ((ulong)edx) << 32;

        Thread.Sleep(500);

        r = Ols.Rdtsc(ref eax, ref edx);
        
        var tsm2 = timeGetTime();

        var diff = (eax | ((ulong)edx) << 32) - tsc;

        var tsd2 = (tsm2 - tsm1) * 1.0;

        rtcspeed = diff / tsd2 / 1000;
      }
    }

    // a thread for the Time Stamp Counter (TSC)
    static void TSCLoop()
    {
      uint eax = 0, edx = 0;

      var sw = Stopwatch.StartNew();

      while (true)
      {
        var ts1 = sw.Elapsed;

        // IA32_TIME_STAMP_COUNTER
        var r = Ols.Rdtsc(ref eax, ref edx);

        var tsc = eax | ((ulong)edx) << 32;

        Thread.Sleep(500);

        r = Ols.Rdtsc(ref eax, ref edx);

        var ts2 = sw.Elapsed;

        var diff = (eax | ((ulong)edx) << 32) - tsc;

        var tsd = (ts2 - ts1).TotalMilliseconds;

        speed = diff / tsd / 1000;
      }
    }


    // last records frequencies, due to threads we just read what ever is here
    static double rtcspeed = 0, speed = 0;

    // get something better that just a simple tickcount.
    [DllImport("Winmm.dll")]
    extern static int timeGetTime();

    bool available = false;

    public IEnumerable<Sample> GetData()
    {
      if (reader == null)
      {
        available = GetCPUInfo();

        if (available)
        {
          //rtcloop = new Thread(RTCLoop);
          //rtcloop.IsBackground = true;
          //rtcloop.Start();

          tscloop = new Thread(TSCLoop);
          tscloop.IsBackground = true;
          tscloop.Start();

          reader = new Thread(ReadLoop);
          reader.IsBackground = true;
          reader.Start();

          Thread.Sleep(1100);
        }
      }

      if (available)
      {
        var data = samples.Clone() as uint[,];

        int r = 0;
        uint eax = 0, edx = 0;

        var temps = new List<double>();

        double multiplier = 0;

        // get max multipler, only works up to max 4 core turbo multiplier
        for (int i = 0; i < CORE_COUNT; i++)
        {
          UIntPtr mask = (UIntPtr)(1 << (i * UIntPtr.Size / 4));
          r = Ols.RdmsrPx(0x198, ref eax, ref edx, mask);
          multiplier += eax & 0xff;
        }

        multiplier /= CORE_COUNT;

        // MSR_PLATFORM_INFO
        // used for speed calculations
        r = Ols.Rdmsr(0xce, ref eax, ref edx);
        var maxnoturboratio = (eax >> 8) & 0xff;

        var fsb = speed / maxnoturboratio;
        var rtcfsb = (rtcspeed == 0 ? speed : rtcspeed) / maxnoturboratio;

        for (int i = 0; i < CORE_COUNT; i++)
        {
          double sample = 0;
          for (int j = 0; j < SAMPLE_RATE; j++)
          {
            sample += GetTemp(data[j, i]);
          }

          temps.Add(sample / SAMPLE_RATE);
        }

        // in Windows we trust...
        yield return Sample.Other("CPU Load", pc.NextValue(), true);

        yield return Sample.Other("Multiplier", multiplier, true);
        yield return Sample.Other("FSB (TSC)", fsb, true);
        //yield return Sample.Other("FSB (RTC)", rtcfsb, false);

        for (int i = 0; i < temps.Count; i++)
        {
          yield return Sample.Temperature("CORE " + (i + 1), temps[i], true);
        }

        yield return Sample.Temperature("CORE AVG", temps.Average(), false);
        yield return Sample.Temperature("CORE MAX", temps.Max(), false);
      }
    }

    static bool GetCPUInfo()
    {
      // vendor
      uint[] reg = new uint[4];
      Ols.Cpuid(0, ref reg[0], ref reg[1], ref reg[2], ref reg[3]);

      var temp = reg[2];
      reg[2] = reg[3];
      reg[3] = temp;

      var buffer = new StringBuilder(12);
      buffer.Length = 12;

      for (int i = 4; i < 16; i++)
      {
        var b = (reg[i / 4] >> (8 * (i % 4))) & 0xFF;
        char c = (char)b;

        buffer[i - 4] = c;
      }

      if (buffer.ToString() == "GenuineIntel")
      {
        Ols.Cpuid(1, ref reg[0], ref reg[1], ref reg[2], ref reg[3]);

        // Processor ID etc
        var Family = ((reg[0] & 0x0FF00000) >> 20) + ((reg[0] & 0x0F00) >> 8);
        var Model = ((reg[0] & 0x0F0000) >> 12) + ((reg[0] & 0xF0) >> 4);

        if (Family == 0x6 && (Model == 0x1e || Model == 0x1a || Model == 0x1f || Model == 0x2e))
        {
          return true;
        }
      }
      return false;
    }

    static double GetTemp(uint eax)
    {
      var temp = tjmax - ((eax >> 16) & 0x7f);
      return temp;
    }

  }

  class SuperIO : IndexedIOPort
  {
    public SuperIO()
    {
      BaseAddress = 0x2e;
      DataPortOffset = 1;
      BankSelect = 0x7;
    }

    const int DEVICE_ID = 0x20;

    public override void Enter()
    {
      Ols.WriteIoPortByte((ushort)AddressPort, 0x87);
      Ols.WriteIoPortByte((ushort)AddressPort, 0x87);
    }

    public override void Exit()
    {
      Ols.WriteIoPortByte((ushort)AddressPort, 0xAA);
    }
    
    public int GetDeviceId()
    {
      Enter();
      var r = ((Read(DEVICE_ID) << 8) | Read(DEVICE_ID + 1));
      Exit();
      return r;
    }

    public int GetBaseAddress()
    {
      Enter();
      var cs = Read(BankSelect);
      Select(0xb);
      var r = ((Read(0x60) << 8) | Read(0x61));
      Select(cs);
      Exit();
      return r;
    }
  }

  // speculation
  // LDN D
  // E1 & E3 = VCORE VID (not)
  // E6 = PLL (66 @ 1.9, 55 @ 2.0, 77 @ 1.8)



  // mostly from linux kernel and docs, and much experimentation
  class W83627DHG : IndexedIOPort, IData, IDisposable, IFanController
  {
    string manufacturer;
    string product;

    public W83627DHG()
    {
      AddressPortOffset = 5;
      DataPortOffset = 6;
      BankSelect = 0x4e;

      if (ProbeForDevice())
      {
        using (var mso = new ManagementObjectSearcher("select * from Win32_BaseBoard"))
        {
          var mos = mso.Get();

          foreach (var obj in mos)
          {
            manufacturer = obj.GetPropertyValue("Manufacturer") as string;
            product = obj.GetPropertyValue("Product") as string;

            break;
          }
        }
       
      }
      else
      {
        throw new Exception("Device not present");
      }
    }

    enum DeviceType
    {
      Unknown = 0,
      W83627EHF = 0x8850,
      W83627EHG = 0x8860,
      W83627DHG = 0xa020,
      W83627DHG_P = 0xb070,
      W83667HG = 0xa510,
      W83667HG_A = 0xb350,
      NotPresent = 0xfff0,
    }

    DeviceType device;
    int sioport = 0;

    public bool ProbeForDevice()
    {
      int[] sioports = { 0x2e, 0x4e };
      var sio = new SuperIO();

      foreach (var port in sioports)
      {
        sio.BaseAddress = port;
        var devid = sio.GetDeviceId() & 0xFFF0;

        if (Enum.IsDefined(typeof(DeviceType), devid))
        {
          device = (DeviceType)devid;

          if (device != DeviceType.NotPresent || device != DeviceType.Unknown)
          {
            // read baseaddress
            BaseAddress = sio.GetBaseAddress();
            sioport = port;

            // reset values
            Select(0);
            Write(0x40, Read(0x40) | (1 << 7));
          }

          return true;
        }
      }
      return false;
    }

    internal void DumpRawByteData(TextWriter writer)
    {
      writer.WriteLine(DateTime.Now);
      writer.Write("     ");

      for (int i = 0; i < 16; i++)
      {
        writer.Write("{0:X2} ", i);
      }

      writer.WriteLine();

      // bank 0
      for (int i = 0; i < 16; i++)
      {
        writer.Write("{0:X3}  ", i * 16);

        for (int j = 0; j < 16; j++)
        {
          int reg = i * 16 + j;

          Select(0);
          var r = Read(reg);

          writer.Write("{0:X2} ", r);
        }
        writer.WriteLine();
      }

      // bank 1 - d
      for (int i = 1; i < 0xe; i++)
      {
        writer.Write("{0:X3}  ", i * 256 + 0x50);

        for (int j = 0; j < 16; j++)
        {
          int reg = 0x50 + j;

          Select(i);
          var r = Read(reg);

          writer.Write("{0:X2} ", r);
        }
        writer.WriteLine();
      }

      writer.WriteLine();

    }

    internal void DumpSIOByteData(TextWriter writer)
    {
      var sio = new SuperIO { BaseAddress = sioport };

      //var css = sio.Read(sio.BankSelect);

      writer.WriteLine(DateTime.Now);
      writer.Write("     ");

      for (int i = 0; i < 16; i++)
      {
        writer.Write("{0:X2} ", i);
      }

      writer.WriteLine();

      for (int i = 7; i < 16; i++)
      {
        for (int j = 0; j < 16; j++)
        {
          writer.Write("{0:X3}  ", i * 256 + j);

          for (int k = 0; k < 16; k++)
          {
            int reg = j * 16 + k;

            sio.Enter();
            sio.Select(i);
            var r = sio.Read(reg);
            sio.Exit();

            writer.Write("{0:X2} ", r);
          }
          writer.WriteLine();
        }
        writer.WriteLine();
      }

      writer.WriteLine();

      //sio.Select(css);
      //sio.Exit();
    }

    static double GetRPM(int fanout, int fandiv)
    {
      return Math.Round(1.35e6 / (fanout * (1 << fandiv)));
    }

    static double lastcputin = double.NaN;

    Dictionary<string, FanSettings> fansettings = new Dictionary<string, FanSettings>
    {
      {
        "CPUFAN0",
        new FanSettings
        {
          Name = "CPUFAN0",
          AutoSpeed = false,
          TempLow = 40,
          TempHigh = 60,
          SpeedLow = 40,
          SpeedHigh = 100,
          SpeedManual = 100,
        }
      },
      {
        "SYSFAN",
        new FanSettings
        {
          Name = "SYSFAN",
          AutoSpeed = true,
          TempLow = 45,
          TempHigh = 60,
          SpeedLow = 40,
          SpeedHigh = 100,
          SpeedManual = 100,
        }
      }
    };

    public IEnumerable<Sample> GetData()
    {
      if (device == DeviceType.Unknown || device == DeviceType.NotPresent)
      {
        yield break;
      }

      double 
        cpufanout0rpm = 0,
        sysfanoutrpm = 0,
        auxfanout0rpm = 0,
        cpufanout1rpm = 0,
        motherboardtemp = 0,
        cputin = 0xFF,
        systin = 0xFF,
        auxtin = 0xFF;

      var cs = Read(BankSelect);

      while (cputin >= 99 || cputin <= -55)
      {
        Select(1);
        cputin = (sbyte)Read(0x50) + (Read(0x51) >> 7) * 0.5;
      }

      while (auxtin >= 99 || auxtin <= -55)
      {
        Select(2);
        auxtin = (sbyte)Read(0x50) + (Read(0x51) >> 7) * 0.5;
      }

      while (systin >= 99 || systin <= -55)
      {
        Select(0);
        systin = (sbyte)Read(0x27);
      }

      if (manufacturer == "ASUSTeK Computer INC." && product.StartsWith("P7P55D"))
      {
        // only on ASUS P7P55D
        var sio = new SuperIO { BaseAddress = sioport };
        
        sio.Enter();
        var css = sio.Read(sio.BankSelect);
        sio.Select(0xc);
        var cr20 = sio.Read(0x20);
        sio.Select(css);
        sio.Exit();

        if (cr20 == 0xb3)
        {
          Select(0);
          Write(0x7d, 0); // seems to be some other values in 1,2,3,4
          motherboardtemp = Read(0x7e);
        }
        else
        {
          motherboardtemp = systin;
        }
      }

      var cputinavg = double.IsNaN(lastcputin) ? cputin : (cputin + lastcputin) / 2;
      lastcputin = cputin;

      var cpufanspeed0 = CalculateFanSpeed(motherboardtemp, cputinavg, fansettings["CPUFAN0"]);
      var sysfanspeed = CalculateFanSpeed(motherboardtemp, cputinavg, fansettings["SYSFAN"]);

      Select(0);
      Write(0x4, 0);
      Write(1, sysfanspeed);
      Write(3, cpufanspeed0);

      var cpufanout0 = Read(0x29);

      int failcount = 0;

      while ((cpufanout0 == 0xff || cpufanout0 == 0) && failcount < 5)
      {
        Thread.Sleep(failcount);
        cpufanout0 = Read(0x29);
        failcount++;
      }

      if (failcount < 5 && cpufanout0 > 0 && cpufanout0 < 255)
      {
        var cpufanout0div = ((Read(0x47) >> 6) & 3) | (((Read(0x5d) >> 6) & 1) << 2);
        cpufanout0rpm = GetRPM(cpufanout0, cpufanout0div);
      }

      failcount = 0;

      var sysfanout = Read(0x28);

      while ((sysfanout == 0xff || sysfanout == 0) && failcount < 5)
      {
        Thread.Sleep(failcount);
        sysfanout = Read(0x28);
        failcount++;
      }

      if (failcount < 5 && sysfanout > 0 && sysfanout < 255)
      {
        var sysfanoutdiv = ((Read(0x47) >> 4) & 3) | (((Read(0x5d) >> 5) & 1) << 2);
        sysfanoutrpm = GetRPM(sysfanout, sysfanoutdiv);
      }

      failcount = 0;

      var auxfanout0 = Read(0x2a);

      while ((auxfanout0 == 0xff || auxfanout0 == 0) && failcount < 5)
      {
        Thread.Sleep(failcount);
        auxfanout0 = Read(0x2a);
        failcount++;
      }

      if (failcount < 5 && auxfanout0 > 0 && auxfanout0 < 255)
      {
        var auxfanout0div = ((Read(0x4b) >> 6) & 3) | (((Read(0x5d) >> 7) & 1) << 2);
        auxfanout0rpm = GetRPM(auxfanout0, auxfanout0div);
      }

      failcount = 0;

      var cpufanout1 = Read(0x3f);

      while ((cpufanout1 == 0xff || cpufanout1 == 0) && failcount < 5)
      {
        Thread.Sleep(failcount);
        cpufanout1 = Read(0x3f);
        failcount++;
      }

      if (failcount < 5 && cpufanout1 > 0 && cpufanout1 < 255)
      {
        var cpufanout1div = ((Read(0x59) >> 0) & 3) | (((Read(0x4c) >> 7) & 1) << 2);
        cpufanout1rpm = GetRPM(cpufanout1, cpufanout1div);

        Debug.Assert(cpufanout1rpm < 100000);
      }

      failcount = 0;

      var vcore = Read(0x20) * 0.008;
      var vin0_12v = Read(0x21) * 0.008 * 7; //guess
      //var avcc = Read(0x22) * 0.008;
      var vcc_3_3v = Read(0x23) * 0.008 * 2; //guess
      var vin1_5v = Read(0x24) * 0.008 * 3; //guess
      //var vin2_2_5v = Read(0x25) * 0.008;
      //var vin3 = Read(0x26) * 0.008;

      Select(cs);

      yield return Sample.Temperature("CPUTIN", cputin, true);
      yield return Sample.Temperature("SYSTIN", systin, false);
      yield return Sample.Temperature("AUXTIN", auxtin, true);

      if (motherboardtemp > 0)
      {
        yield return Sample.Temperature("Motherboard", motherboardtemp, true);
      }

      yield return Sample.FanSpeed("CPUFAN0", cpufanout0rpm, true);
      yield return Sample.FanSpeed("CPUFAN1", cpufanout1rpm, true);
      yield return Sample.FanSpeed("SYSFAN", sysfanoutrpm, true);
      yield return Sample.FanSpeed("AUXFAN", auxfanout0rpm, true);

      yield return Sample.Voltage("VCORE", vcore, true);
      yield return Sample.Voltage("12V", vin0_12v, false);
      yield return Sample.Voltage("5V", vin1_5v, false);
      yield return Sample.Voltage("3.3V", vcc_3_3v, false);
    }

    static int CalculateFanSpeed(double motherboardtemp, double cputinavg, FanSettings fs)
    {
      const double DIV = 100.0 / 255;

      if (fs.AutoSpeed)
      {
        var OFFSET = motherboardtemp - 27;
        var SOFFSET = OFFSET * 2;
        var TOFFSET = OFFSET / 2;
        
        // max speed = 100%
        var speed = 0xff;
        // min speed = 35%
        var MINSPEED = Math.Max(45/DIV, (fs.SpeedLow + SOFFSET)/ DIV);
        var MAXSPEED = (fs.SpeedHigh) / DIV;

        var BOTTOMTEMP = fs.TempLow + TOFFSET;
        var TOPTEMP = fs.TempHigh;

        if (cputinavg < BOTTOMTEMP)
        {
          // min speed = 35%
          speed = (int)MINSPEED;
        }
        else if (cputinavg < TOPTEMP)
        {
          // from 45 - 70 deg slope from 35% - 100%
          speed = Math.Min(255, (int)(MINSPEED + (cputinavg - BOTTOMTEMP) * (MAXSPEED - MINSPEED) / (TOPTEMP - BOTTOMTEMP)));
        }
        return speed;
      }
      else
      {
        return (int)(fs.SpeedManual / DIV);
      }
    }

    #region IDisposable Members

    ~W83627DHG()
    {
      GC.SuppressFinalize(this);
      Dispose();
    }

    public void Dispose()
    {
      Enter();
      Select(0);

      Write(1, 0xff);
      Write(3, 0xff);

      Exit();
    }

    #endregion

    #region IFanController Members

    public IEnumerable<FanSettings> GetSettings()
    {
      return fansettings.Values;
    }

    public void UpdateSettings(FanSettings fs)
    {
      fansettings[fs.Name] = fs;
    }

    #endregion
  }

#if NVIDIA
  class NVidiaGPU : IData
  {
    bool available;
    

    NvPhysicalGpuHandle[] handles;

    public NVidiaGPU()
    {
      //NvAPI.Initialize();
      //var err = NvAPI.GetErrorMessage(Status.FILE_NOT_FOUND);

      if (available = NVAPI.IsAvailable)
      {
        handles = new NvPhysicalGpuHandle[NVAPI.MAX_PHYSICAL_GPUS * 4];

        NvAPI.Initialize();

        var dh = NvAPI.GetAssociatedNvidiaDisplayHandle(@"\\.\DISPLAY1");

        int count;
        var r = NVAPI.NvAPI_EnumPhysicalGPUs(handles, out count);
        if (r != NvStatus.OK)
        {
          NvDisplayHandle dhnd = new NvDisplayHandle();

          for (int i = 0; i < NVAPI.MAX_PHYSICAL_GPUS * 2; i++)
          {
            var h = NvAPI.EnumNvidiaUnAttachedDisplayHandle(i);

            if (h != IntPtr.Zero)
            {
              Console.WriteLine("Found dev at #{0} ({1:X8})", i, dhnd);
            }

            // r = NVAPI.NvAPI_EnumNvidiaDisplayHandle(i, ref dhnd);
            //if (r != NvStatus.OK)
            //{
            //  continue;
            //}
            //else
            //{
            //  Console.WriteLine("Found dev at #{0} ({1:X8})", i, dhnd);
            //}
          }
        }
      }
    }

    static NvGPUThermalSettings GetThermalSettings(NvPhysicalGpuHandle handle)
    {
      NvGPUThermalSettings settings = new NvGPUThermalSettings();
      settings.Version = NVAPI.GPU_THERMAL_SETTINGS_VER;
      settings.Count = NVAPI.MAX_THERMAL_SENSORS_PER_GPU;
      settings.Sensor = new NvSensor[NVAPI.MAX_THERMAL_SENSORS_PER_GPU];
      if (NVAPI.NvAPI_GPU_GetThermalSettings(handle, (int)NvThermalTarget.ALL,
        ref settings) != NvStatus.OK)
      {
        settings.Count = 0;
      }
      return settings;
    }

    public IEnumerable<Sample> GetData()
    {
      if (!available)
      {
        yield break;
      }

      foreach (var handle in handles)
      {
        string name;
        NVAPI.NvAPI_GPU_GetFullName(handle, out name);

        NvGPUThermalSettings settings = GetThermalSettings(handle);

        for (int i = 0; i < settings.Count; i++)
        {
          NvSensor sensor = settings.Sensor[i];
          switch (sensor.Target)
          {
            case NvThermalTarget.BOARD: 
              name = "GPU Board"; 
              break;
            case NvThermalTarget.GPU: 
              name = "GPU Core";
              break;
            case NvThermalTarget.MEMORY: 
              name = "GPU Memory"; 
              break;
            case NvThermalTarget.POWER_SUPPLY: 
              name = "GPU Power Supply"; 
              break;
            case NvThermalTarget.UNKNOWN: 
              name = "GPU Unknown"; 
              break;
            default: 
              name = "GPU"; 
              break;
          }

          yield return Sample.Temperature(name, sensor.CurrentTemp, true);
        }

        int value;
        if (NVAPI.NvAPI_GPU_GetTachReading != null &&
          NVAPI.NvAPI_GPU_GetTachReading(handle, out value) == NvStatus.OK)
        {
          yield return Sample.FanSpeed("GPU Fan", value, true);
        }
      }
    }

    
  }
#endif
}
