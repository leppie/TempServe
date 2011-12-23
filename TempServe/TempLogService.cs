using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Runtime.Remoting;

namespace TempServe
{
  partial class TempLogService : ServiceBase
  {
    public TempLogService()
    {
      InitializeComponent();
    }

    Thread mainloop;
    IpcServerChannel server;

    protected override void OnStart(string[] args)
    {
      var sbs = new BinaryServerFormatterSinkProvider();

      var props = new Dictionary<string, object>()
      {
        { "name", "TempServe" },
        { "portName", "TempServe" },
        { "authorizedGroup", "Users" }
      };

      server = new IpcServerChannel(props, sbs );

      ChannelServices.RegisterChannel(server, false);

      RemotingConfiguration.RegisterWellKnownServiceType(typeof(TempLogger), "DataService", WellKnownObjectMode.Singleton);

      TempLogger.running = true;
      mainloop = new Thread(() => TempLogger.StartTempLogging(false, true, false));
      mainloop.Start();
    }

    protected override void OnStop()
    {
      TempLogger.running = false;

      ChannelServices.UnregisterChannel(server);

      mainloop.Join();
      mainloop = null;
    }
  }
}
