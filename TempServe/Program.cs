//-----------------------------------------------------------------------------
//     Author : hiyohiyo
//       Mail : hiyohiyo@crystalmark.info
//        Web : http://openlibsys.org/
//    License : The modified BSD license
//
//                          Copyright 2007 OpenLibSys.org. All rights reserved.
//-----------------------------------------------------------------------------

using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Runtime.InteropServices;

using OpenLibSys;
using System.Threading;
using System.IO;
using System.Data.SqlServerCe;
using System.Diagnostics;
using System.ServiceProcess;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Management;
using System.Configuration.Install;

namespace TempServe
{
  class Program
  {
    /// <summary>
    /// Main Entry Point
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
      bool consolemode = false;
      bool logmode = false;
      bool dumpmode = false;

      if (args.Contains("-install"))
      {
        try
        {
          string[] a = { "TempServe.exe" };
          ManagedInstallerClass.InstallHelper(a);
        }
        catch (Exception exception)
        {
          Console.Error.WriteLine(exception.Message);
          return -1;
        }

        return 0;
      }

      if (args.Contains("-uninstall"))
      {
        try
        {
          string[] a = { "/u", "TempServe.exe" };
          ManagedInstallerClass.InstallHelper(a);
        }
        catch (Exception exception)
        {
          Console.Error.WriteLine(exception.Message);
          return -1;
        }

        return 0;
      }

      if (args.Contains("-console"))
      {
        consolemode = true;
      }
      
      if (args.Contains("-log"))
      {
        logmode = true;
      }

      if (args.Contains("-dump"))
      {
        dumpmode = true;
      }

      try
      {
        if (!consolemode)
        {
          logmode = true;

          ServiceBase.Run(new TempLogService());
        }
        else
        {
          TempLogger.StartTempLogging(consolemode, logmode, dumpmode);
        }

        
      }
      catch (Exception ex)
      {
        Console.Error.WriteLine(ex);
        return -1;
      }

      return 0;
    }
  }
}