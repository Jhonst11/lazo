using System.Reflection;
using Microsoft.Win32;

namespace Lazo
{
    internal static class Startup
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string Name = "Lazo";

        public static bool IsEnabled()
        {
            if (Has(Registry.CurrentUser)) return true;
            using (RegistryKey machine = Machine()) return Has(machine);
        }

        public static void Set(bool enabled)
        {
            string path = "\"" + Assembly.GetExecutingAssembly().Location + "\"";
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled) key.SetValue(Name, path);
                else key.DeleteValue(Name, false);
            }
            try
            {
                using (RegistryKey key = Machine())
                {
                    if (key == null) return;
                    using (RegistryKey run = key.OpenSubKey(RunKey, true))
                    {
                        if (run == null) return;
                        if (enabled) run.SetValue(Name, path);
                        else run.DeleteValue(Name, false);
                    }
                }
            }
            catch { }
        }

        private static bool Has(RegistryKey root)
        {
            if (root == null) return false;
            using (RegistryKey key = root.OpenSubKey(RunKey))
                return key != null && key.GetValue(Name) != null;
        }

        private static RegistryKey Machine()
        {
            try
            {
                return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
                    System.Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);
            }
            catch { return null; }
        }
    }
}
