using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lazo
{
    internal static class Updater
    {
        public const string Repo = "maxhine/lazo";
        private static readonly Version Current = Assembly.GetExecutingAssembly().GetName().Version;

        public static bool Enabled { get; private set; }

        public static void Load()
        {
            try { Enabled = File.ReadAllText(Path()).Trim() != "0"; }
            catch { Enabled = true; }
        }

        public static void Set(bool enabled, bool persist)
        {
            Enabled = enabled;
            if (!persist) return;
            try
            {
                string path = Path();
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                File.WriteAllText(path, enabled ? "1" : "0");
            }
            catch { }
        }

        public static void CheckInBackground(Action<string> report, Action shutdown)
        {
            if (!Enabled) return;
            Check(report, shutdown);
        }

        public static void Check(Action<string> report, Action shutdown)
        {
            Task.Run(() =>
            {
                try
                {
                    report("Buscando actualizaciones…");
                    string setup = DownloadNewer();
                    if (setup == null)
                    {
                        report("Lazo está actualizado.");
                        return;
                    }
                    report("Instalando actualización…");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = setup,
                        Arguments = "/update",
                        UseShellExecute = true
                    });
                    shutdown();
                }
                catch
                {
                    report("No se pudo comprobar.");
                }
            });
        }

        private static string DownloadNewer()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            string json;
            using (WebClient client = Client())
                json = client.DownloadString("https://api.github.com/repos/" + Repo + "/releases/latest");
            Match tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?([0-9]+\\.[0-9]+\\.[0-9]+)\"");
            Version remote;
            if (!tag.Success || !Version.TryParse(tag.Groups[1].Value, out remote) || remote <= Current) return null;
            Match asset = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"(https:[^\"]*Lazo-Setup-[^\"]+\\.exe)\"");
            if (!asset.Success) return null;
            string url = asset.Groups[1].Value.Replace("\\u0026", "&");
            if (!Allowed(url)) return null;
            string file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Lazo-Setup-" + remote + ".exe");
            using (WebClient client = Client()) client.DownloadFile(url, file);
            return file;
        }

        private static WebClient Client()
        {
            WebClient client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = "Lazo";
            client.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            return client;
        }

        private static bool Allowed(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) || uri.Scheme != "https") return false;
            string host = uri.Host;
            return host == "github.com" || host == "objects.githubusercontent.com" ||
                   host == "release-assets.githubusercontent.com" || host.EndsWith(".githubusercontent.com");
        }

        private static string Path()
        {
            return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lazo", "updates.txt");
        }
    }
}
