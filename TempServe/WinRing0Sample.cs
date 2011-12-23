//-----------------------------------------------------------------------------
//     Author : hiyohiyo
//       Mail : hiyohiyo@crystalmark.info
//        Web : http://openlibsys.org/
//    License : The modified BSD license
//
//                     Copyright 2007-2009 OpenLibSys.org. All rights reserved.
//-----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Security;
using System.Security.Principal;

using OpenLibSys;

namespace WinRing0SampleCs
{
    public partial class WinRing0Sample : Form
    {
        public WinRing0Sample()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (!IsAdmin())
            {
                RunAsRestart();
                Environment.Exit(0);
            }
            CenterToScreen();
            Init();
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            Init();
        }

        private bool IsAdmin()
        {
            OperatingSystem osInfo = Environment.OSVersion;
            if (osInfo.Platform == PlatformID.Win32Windows)
            {
                return true;
            }
            else
            {
                WindowsIdentity usrId = WindowsIdentity.GetCurrent();
                WindowsPrincipal p = new WindowsPrincipal(usrId);
                return p.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private bool RunAsRestart()
        {
            string[] args = Environment.GetCommandLineArgs();

            foreach (string s in args)
            {
                if (s.Equals("runas"))
                {
                    return false;
                }
            }
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = true;
            startInfo.WorkingDirectory = Environment.CurrentDirectory;
            startInfo.FileName = Application.ExecutablePath;
            startInfo.Verb = "runas";
            startInfo.Arguments = "runas";

            try
            {
                Process.Start(startInfo);
            }
            catch
            {
                return false;
            }
            return true;
        }

        unsafe private void Init()
        {
            this.textBox.Text = "";
            this.textBox.Refresh();

            Cursor.Current = Cursors.WaitCursor;

            try
            {
                //-----------------------------------------------------------------------------
                // Initialize
                //-----------------------------------------------------------------------------
                Ols ols = new Ols();

                // Check support library sutatus
                switch (ols.GetStatus())
                {
                    case (uint)Ols.Status.NO_ERROR:
                        break;
                    case (uint)Ols.Status.DLL_NOT_FOUND:
                        MessageBox.Show("Status Error!! DLL_NOT_FOUND");
                        Environment.Exit(0);
                        break;
                    case (uint)Ols.Status.DLL_INCORRECT_VERSION:
                        MessageBox.Show("Status Error!! DLL_INCORRECT_VERSION");
                        Environment.Exit(0);
                        break;
                    case (uint)Ols.Status.DLL_INITIALIZE_ERROR:
                        MessageBox.Show("Status Error!! DLL_INITIALIZE_ERROR");
                        Environment.Exit(0);
                        break;
                }

                // Check WinRing0 status
                switch (ols.GetDllStatus())
                {
                    case (uint)Ols.OlsDllStatus.OLS_DLL_NO_ERROR:
                        break;
                    case (uint)Ols.OlsDllStatus.OLS_DLL_DRIVER_NOT_LOADED:
                        MessageBox.Show("DLL Status Error!! OLS_DRIVER_NOT_LOADED");
                        Environment.Exit(0);
                        break;
                    case (uint)Ols.OlsDllStatus.OLS_DLL_UNSUPPORTED_PLATFORM:
                        MessageBox.Show("DLL Status Error!! OLS_UNSUPPORTED_PLATFORM");
                        Environment.Exit(0);
                        break;
                    case (uint)Ols.OlsDllStatus.OLS_DLL_DRIVER_NOT_FOUND:
                        MessageBox.Show("DLL Status Error!! OLS_DLL_DRIVER_NOT_FOUND");
                        Environment.Exit(0);
                        break;
                    case (uint)Ols.OlsDllStatus.OLS_DLL_DRIVER_UNLOADED:
                        MessageBox.Show("DLL Status Error!! OLS_DLL_DRIVER_UNLOADED");
                        Environment.Exit(0);
                        break;
                    case (uint)Ols.OlsDllStatus.OLS_DLL_DRIVER_NOT_LOADED_ON_NETWORK:
                        MessageBox.Show("DLL Status Error!! DRIVER_NOT_LOADED_ON_NETWORK");
                        Environment.Exit(0);
                        break;
                    case (uint)Ols.OlsDllStatus.OLS_DLL_UNKNOWN_ERROR:
                        MessageBox.Show("DLL Status Error!! OLS_DLL_UNKNOWN_ERROR");
                        Environment.Exit(0);
                        break;
                }

                String str = "";
                //-----------------------------------------------------------------------------
                // DLL Information
                //-----------------------------------------------------------------------------
                byte major = 0, minor = 0, revision = 0, release = 0;

                str += "[DLL Version]\r\n";
                ols.GetDllVersion(ref major, ref minor, ref revision, ref release);
                str += major.ToString()
                    + "." + minor.ToString()
                    + "." + revision.ToString()
                    + "." + release.ToString()
                    + "\r\n";

                str += "[Device Driver Version]\r\n";
                ols.GetDriverVersion(ref major, ref minor, ref revision, ref release);
                str += major.ToString()
                    + "." + minor.ToString()
                    + "." + revision.ToString()
                    + "." + release.ToString()
                    + "\r\n";

                str += "[Device Driver Type]\r\n";
                switch (ols.GetDriverType())
                {
                    case (uint)Ols.OlsDriverType.OLS_DRIVER_TYPE_WIN_9X:
                        str += "OLS_DRIVER_TYPE_WIN_9X\r\n";
                        break;
                    case (uint)Ols.OlsDriverType.OLS_DRIVER_TYPE_WIN_NT:
                        str += "OLS_DRIVER_TYPE_WIN_NT\r\n";
                        break;
                    case (uint)Ols.OlsDriverType.OLS_DRIVER_TYPE_WIN_NT_X64:
                        str += "OLS_DRIVER_TYPE_WIN_NT_X64\r\n";
                        break;
                    case (uint)Ols.OlsDriverType.OLS_DRIVER_TYPE_WIN_NT_IA64:
                        str += "OLS_DRIVER_TYPE_WIN_NT_IA64\r\n";
                        break;
                    default:
                        str += "OLS_DRIVER_TYPE_UNKNOWN\r\n";
                        break;
                }

                //-----------------------------------------------------------------------------
                // TSC
                //-----------------------------------------------------------------------------
                uint index = 0, eax = 0, ebx = 0, ecx = 0, edx = 0;

                str += "[TSC]\r\n";
                if (ols.RdtscPx(ref eax, ref edx, (UIntPtr)1) != 0)
                {
                    str += "index     63-32    31-0\r\n";
                    str += index.ToString("X8") + ": " + edx.ToString("X8")
                        + " " + eax.ToString("X8") + "\r\n";
                }
                else
                {
                    str += "Failure : Change Process Affinity Mask\r\n";
                }

                //-----------------------------------------------------------------------------
                // MSR
                //-----------------------------------------------------------------------------
                str += "[MSR]\r\n";
                index = 0x00000010; // Time Stamp Counter
                if (ols.RdmsrTx(index, ref eax, ref edx, (UIntPtr)1) != 0)
                {
                    str += "index     63-32    31-0\r\n";
                    str += index.ToString("X8") + ": " + edx.ToString("X8")
                        + " " + eax.ToString("X8") + "\r\n";
                }
                else
                {
                    str += "Failure : Change Thread Affinity Mask\r\n";
                }

                //-----------------------------------------------------------------------------
                // CPUID (Standard/Extended)
                //-----------------------------------------------------------------------------
                uint maxCpuid = 0, maxCpuidEx = 0;

                str += "[CPUID]\r\n";
                str += "index     EAX      EBX      ECX      EDX  \r\n";

                // Standard
                ols.CpuidPx(0x00000000, ref maxCpuid, ref ebx, ref ecx, ref edx, (UIntPtr)1);
                for (index = 0x00000000; index <= maxCpuid; index++)
                {
                    ols.CpuidPx(index, ref eax, ref ebx, ref ecx, ref edx, (UIntPtr)1);
                    str += index.ToString("X8") + ": "
                        + eax.ToString("X8") + " " + ebx.ToString("X8") + " "
                        + ecx.ToString("X8") + " " + edx.ToString("X8") + "\r\n";
                }
                // Extended
                ols.CpuidPx(0x80000000, ref maxCpuidEx, ref ebx, ref ecx, ref edx, (UIntPtr)1);
                for (index = 0x80000000; index <= maxCpuidEx; index++)
                {
                    ols.CpuidPx(index, ref eax, ref ebx, ref ecx, ref edx, (UIntPtr)1);
                    str += index.ToString("X8") + ": "
                        + eax.ToString("X8") + " " + ebx.ToString("X8") + " "
                        + ecx.ToString("X8") + " " + edx.ToString("X8") + "\r\n";
                }

                //-----------------------------------------------------------------------------
                // I/O (Beep)
                //-----------------------------------------------------------------------------


                var port = new IndexedIOPort
                {
                  BaseAddress = 0x290,
                  AddressPortOffset = 5,
                  DataPortOffset = 6,
                  BankSelect = 0x4e
                };


                port.Select(1);
                var cputin = port.Read(0x50) + (port.Read(0x51) >> 7) * 0.5;

                port.Select(2);
                var auxtin = port.Read(0x50) + (port.Read(0x51) >> 7) * 0.5;

                port.Select(0);
                var systin = port.Read(0x27);

                var cpufanout0 = port.Read(0x29);
                var cpufanout0div = ((port.Read(0x47) >> 6) & 3) | (((port.Read(0x5d) >> 6) & 1) << 2);

                var cpufanout0rpm = 1.35e6 / (cpufanout0 * (1 << cpufanout0div));

                var sysfanout = port.Read(0x28);
                var sysfanoutdiv = ((port.Read(0x47) >> 4) & 3) | (((port.Read(0x5d) >> 5) & 1) << 2); ;

                var sysfanoutrpm = 1.35e6 / (sysfanout * (1 << sysfanoutdiv));

                var auxfanout0 = port.Read(0x2a);
                var auxfanout0div = ((port.Read(0x4b) >> 6) & 3) | (((port.Read(0x5d) >> 7) & 1) << 2); ;

                var auxfanout0rpm = 1.35e6 / (auxfanout0 * (1 << auxfanout0div));

                var cpufanout1 = port.Read(0x3f);
                var cpufanout1div = ((port.Read(0x59) >> 0) & 3) | (((port.Read(0x4c) >> 7) & 1) << 2);

                var cpufanout1rpm = 1.35e6 / (cpufanout1 * (1 << cpufanout1div));

                port.Select(5);
                var auxfanout1 = port.Read(0x53);
                port.Select(0);
                var auxfanout1div = (port.Read(0x59) >> 2) & 3;

                var vcore = port.Read(0x20) * 0.008;
                var vin0_12v = port.Read(0x21) * 0.008 * 7;
                var avcc = port.Read(0x22) * 0.008;
                var vcc_3_3v = port.Read(0x23) * 0.008 * 2;
                var vin1_5v = port.Read(0x24) * 0.008 * 3;
                var vin2_2_5v = port.Read(0x25) * 0.008;
                var vin3 = port.Read(0x26) * 0.008;


                //ols.WriteIoPortByte(LPCINDEX, 0xFE);
                //var dts = ols.ReadIoPortByte(LPCDATA);


                //uint freq = 1193180000 / 440000; // 440Hz

                //ols.WriteIoPortByte(0x43, 0xB6);
                //ols.WriteIoPortByte(0x42, (byte)(freq & 0xFF));
                //ols.WriteIoPortByte(0x42, (byte)(freq >> 9));

                //System.Threading.Thread.Sleep(100);

                //ols.WriteIoPortByte(0x61, (byte)(ols.ReadIoPortByte(0x61) | 0x03));

                //System.Threading.Thread.Sleep(1000);

                //ols.WriteIoPortByte(0x61, (byte)(ols.ReadIoPortByte(0x61) & 0xFC));

                //str += "[I/O]\r\nBeep 440Hz\r\n";

                //-----------------------------------------------------------------------------
                // PCI
                //-----------------------------------------------------------------------------
                uint address, value;

                str += "[PCI]\r\n";

                // All Device
                str += "Bus Dev Fnc VendorDevice\r\n";
                for (uint bus = 0; bus <= 128; bus++)
                {
                    for (uint dev = 0; dev < 32; dev++)
                    {
                        for (uint func = 0; func < 8; func++)
                        {
                            address = ols.PciBusDevFunc(bus, dev, func);
                            value = ols.ReadPciConfigDword(address, 0x00);
                            if ((value & 0xFFFF) != 0xFFFF && (value & 0xFFFF) != 0x0000)
                            {
                                str += ols.PciGetBus(address).ToString("X2") + "h "
                                    + ols.PciGetDev(address).ToString("X2") + "h "
                                    + ols.PciGetFunc(address).ToString("X2") + "h "
                                    + ((uint)(value & 0x0000FFFF)).ToString("X04") + "h "
                                    + ((uint)((value >> 16) & 0x0000FFFF)).ToString("X04") + "h\r\n";
                            }
                        }
                    }
                }

                // Host Bridge
                address = ols.FindPciDeviceByClass( 0x06, 0x00, 0x00, 0);
                if (address != 0xFFFFFFFF)
                {
                    str += "[PCI Confguration Space Dump] HostBridge\r\n";
                    str += "    00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F\r\n";
                    str += "---------------------------------------------------\r\n";

                    for (int i = 0; i < 256; i += 16)
                    {
                        str += i.ToString("X2") + "|";
                        for (int j = 0; j < 16; j++)
                        {
                            str += " " + (ols.ReadPciConfigByte(address, (byte)(i + j))).ToString("X2");
                        }
                        str += "\r\n";
                    }
                }

                this.textBox.Text = str;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
                Environment.Exit(0);
            }

            Cursor.Current = Cursors.Default;
        }
    }
}