using System;
using System.Threading;
using System.Windows;
using System.Net;

namespace Lazo
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool previewReceive = Array.IndexOf(args, "--preview-receive") >= 0 ||
                                  Array.IndexOf(args, "--preview-receive-glass") >= 0;
            if (previewReceive)
            {
                Theme.Load();
                if (Array.IndexOf(args, "--preview-receive-glass") >= 0) Theme.SetForPreview(ThemeKind.Glass);
                Offer offer = new Offer { Id = Guid.NewGuid(), Sender = "EQUIPO-OFICINA",
                    Address = IPAddress.Parse("192.168.1.12"), FileName = "proyecto.pdf", Size = 3429018 };
                Application receiveApp = new Application();
                receiveApp.Run(new ReceiveWindow(offer, accepted => { }));
                return;
            }
            bool preview = Array.IndexOf(args, "--preview") >= 0 || Array.IndexOf(args, "--preview-glass") >= 0;
            if (preview)
            {
                Application previewApp = new Application();
                previewApp.Run(new MainWindow(true, Array.IndexOf(args, "--preview-glass") >= 0));
                return;
            }
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
