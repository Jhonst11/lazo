using System;
using System.Threading;
using System.Windows;

namespace Lazo
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool first;
            using (Mutex instance = new Mutex(true, "Local\\Lazo.Transfer.App", out first))
            {
                if (!first)
                {
                    try { using (EventWaitHandle signal = EventWaitHandle.OpenExisting("Local\\Lazo.Transfer.Show")) signal.Set(); }
                    catch { }
                    return;
                }
                using (EventWaitHandle signal = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\Lazo.Transfer.Show"))
                {
                    Application app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    MainWindow window = new MainWindow();
                    Thread listener = new Thread(() =>
                    {
                        while (true)
                        {
                            signal.WaitOne();
                            try { window.Dispatcher.BeginInvoke((Action)window.ActivateFromElsewhere); }
                            catch { return; }
                        }
                    });
                    listener.IsBackground = true;
                    listener.Start();
                    window.Show();
                    app.Run();
                }
            }
        }
    }
}
