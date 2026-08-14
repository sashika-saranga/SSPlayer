using System;
using System.Threading.Tasks;
using System.Windows;

namespace Mp3Player
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // initialize logger and cleanup old logs
            try { Logger.Initialize(); } catch { }

            // global exception handlers
            this.DispatcherUnhandledException += (s, ex) =>
            {
                try { Logger.Log(ex.Exception, "DispatcherUnhandledException"); } catch { }
            };

            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                try
                {
                    if (ex.ExceptionObject is Exception eobj) Logger.Log(eobj, "AppDomain UnhandledException");
                    else Logger.Log($"Unhandled exception object: {ex.ExceptionObject}");
                }
                catch { }
            };

            TaskScheduler.UnobservedTaskException += (s, ex) =>
            {
                try { Logger.Log(ex.Exception, "UnobservedTaskException"); } catch { }
            };
        }
    }
}
