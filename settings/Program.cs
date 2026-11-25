namespace MxHaptics.Settings
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Windows.Forms;

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            using var mutex = new Mutex(true, @"Local\MxHapticsSettings", out var createdNew);
            if (!createdNew)
            {
                ActivateExistingWindow();
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.SetColorMode(SystemColorMode.System);
            Application.Run(new SettingsForm());
        }

        private static void ActivateExistingWindow()
        {
            var current = Process.GetCurrentProcess();
            foreach (var p in Process.GetProcessesByName(current.ProcessName))
            {
                if (p.Id != current.Id && p.MainWindowHandle != IntPtr.Zero)
                {
                    ShowWindow(p.MainWindowHandle, 9 /* SW_RESTORE */);
                    SetForegroundWindow(p.MainWindowHandle);
                    return;
                }
            }
        }

        [DllImport("user32.dll")]
        private static extern Boolean SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern Boolean ShowWindow(IntPtr hwnd, Int32 cmd);
    }
}
