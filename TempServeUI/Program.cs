using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.ServiceProcess;
using System.Threading;

namespace TempServe
{
  static class Program
  {
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);

      var sc = new ServiceController("TempServe");

      try
      {
        var sts = sc.Status;
        if (sts != ServiceControllerStatus.Running)
        {
          if (MessageBox.Show("Start TempServe service?", "TempServe service not running", 
            MessageBoxButtons.OKCancel, MessageBoxIcon.Question)
            == DialogResult.OK)
          {
            sc.Start();

            Thread.Sleep(2000);
          }
          else
          {
            return;
          }
        }
      }
      catch (InvalidOperationException)
      {
        //service not installed
        MessageBox.Show("TempServe service not installed", "Error", 
          MessageBoxButtons.OK, MessageBoxIcon.Error);
        return;
      }
      
        

      Application.Run(new TempServeUI());
    }
  }
}
