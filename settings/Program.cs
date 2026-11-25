namespace MxHaptics.Settings
{
    using System;
    using System.Diagnostics;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Windows.Forms;

    internal static class Program
    {
        /// <summary>
        /// Běžně otevře okno nastavení (jedna instance).
        /// <c>--screenshot &lt;soubor.png&gt; [--theme light|dark]</c> okno jen vykreslí do PNG a zavře se
        /// (pro dokumentaci; nic neukládá a nekontroluje jedinou instanci).
        /// </summary>
        [STAThread]
        private static Int32 Main(String[] args)
        {
            var screenshotPath = ArgValue(args, "--screenshot");
            var theme = ArgValue(args, "--theme");

            if (screenshotPath != null)
            {
                ApplicationConfiguration.Initialize();
                Application.SetColorMode(theme switch
                {
                    "light" => SystemColorMode.Classic,
                    "dark" => SystemColorMode.Dark,
                    _ => SystemColorMode.System,
                });
                return RunScreenshot(Path.GetFullPath(screenshotPath));
            }

            using var mutex = new Mutex(true, @"Local\MxHapticsSettings", out var createdNew);
            if (!createdNew)
            {
                ActivateExistingWindow();
                return 0;
            }

            ApplicationConfiguration.Initialize();
            Application.SetColorMode(SystemColorMode.System);
            Application.Run(new SettingsForm());
            return 0;
        }

        private static String ArgValue(String[] args, String name)
        {
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private static Int32 RunScreenshot(String path)
        {
            var exitCode = 1;
            using var form = new SettingsForm(readOnly: true) { ShowInTaskbar = false };
            form.Shown += (_, _) =>
            {
                // nechat okno doopravdy vykreslit (DWM rámeček, tmavý režim), pak zachytit i s titulkem
                var timer = new System.Windows.Forms.Timer { Interval = 600 };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    try
                    {
                        CaptureWindow(form.Handle, path);
                        exitCode = 0;
                    }
                    finally
                    {
                        form.Close();
                    }
                };
                timer.Start();
            };
            Application.Run(form);
            return exitCode;
        }

        private static void CaptureWindow(IntPtr hwnd, String path)
        {
            // viditelné okno bez neviditelného okraje pro změnu velikosti
            if (DwmGetWindowAttribute(hwnd, 9 /* DWMWA_EXTENDED_FRAME_BOUNDS */, out var frame, Marshal.SizeOf<RECT>()) != 0)
            {
                GetWindowRect(hwnd, out frame);
            }

            GetWindowRect(hwnd, out var window);
            using var full = new Bitmap(window.Right - window.Left, window.Bottom - window.Top, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(full))
            {
                var hdc = g.GetHdc();
                PrintWindow(hwnd, hdc, 2 /* PW_RENDERFULLCONTENT */);
                g.ReleaseHdc(hdc);
            }

            var crop = new Rectangle(frame.Left - window.Left, frame.Top - window.Top, frame.Right - frame.Left, frame.Bottom - frame.Top);
            using var cropped = full.Clone(crop, PixelFormat.Format32bppArgb);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            cropped.Save(path, ImageFormat.Png);
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

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public Int32 Left;
            public Int32 Top;
            public Int32 Right;
            public Int32 Bottom;
        }

        [DllImport("user32.dll")]
        private static extern Boolean SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern Boolean ShowWindow(IntPtr hwnd, Int32 cmd);

        [DllImport("user32.dll")]
        private static extern Boolean GetWindowRect(IntPtr hwnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern Boolean PrintWindow(IntPtr hwnd, IntPtr hdc, UInt32 flags);

        [DllImport("dwmapi.dll")]
        private static extern Int32 DwmGetWindowAttribute(IntPtr hwnd, Int32 attribute, out RECT value, Int32 size);
    }
}
