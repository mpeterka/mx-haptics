namespace Loupedeck.MxHapticsPlugin
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Threading;

    using MxHaptics.Shared;

    /// <summary>
    /// Sleduje kurzor a hlásí události: přechod mezi okny, najetí na klikatelný prvek, náraz na okraj plochy.
    /// Dvě vlákna: levné sledování oken/okrajů (10 ms) a pomalejší hit-test prvků (30 ms; MSAA, na hlavní liště UIA),
    /// aby zaseknutá aplikace pod kurzorem nezdržela hranice oken.
    /// </summary>
    internal sealed class PointerWatcher : IDisposable
    {
        private const Int32 WindowPollMs = 10;
        private const Int32 ControlPollMs = 30;
        private const Int64 WindowDebounceMs = 90;
        private const Int64 EdgeDebounceMs = 150;
        private const Int64 ControlDebounceMs = 60;

        // MSAA role, které považujeme za "tlačítko"
        private static readonly Int32[] ClickableRoles =
        {
            0x0C, // ROLE_SYSTEM_MENUITEM
            0x1E, // ROLE_SYSTEM_LINK
            0x25, // ROLE_SYSTEM_PAGETAB
            0x2B, // ROLE_SYSTEM_PUSHBUTTON
            0x2C, // ROLE_SYSTEM_CHECKBUTTON
            0x2D, // ROLE_SYSTEM_RADIOBUTTON
            0x2E, // ROLE_SYSTEM_COMBOBOX
            0x38, // ROLE_SYSTEM_BUTTONDROPDOWN
            0x39, // ROLE_SYSTEM_BUTTONMENU
            0x3E, // ROLE_SYSTEM_SPLITBUTTON
        };

        private readonly Action<String> _raise;
        private readonly CancellationTokenSource _cts = new();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private Thread _windowThread;
        private readonly Func<Boolean> _taskbarEnabled;
        private Thread _controlThread;

        public PointerWatcher(Action<String> raise, Func<Boolean> taskbarEnabled)
        {
            this._raise = raise;
            this._taskbarEnabled = taskbarEnabled;
        }

        public void Start()
        {
            this._windowThread = new Thread(this.WindowLoop) { IsBackground = true, Name = "MxHaptics.Window" };
            this._controlThread = new Thread(this.ControlLoop) { IsBackground = true, Name = "MxHaptics.Control" };
            this._controlThread.SetApartmentState(ApartmentState.STA);
            this._windowThread.Start();
            this._controlThread.Start();
        }

        public void Dispose()
        {
            this._cts.Cancel();
            this._windowThread?.Join(500);
            this._controlThread?.Join(500);
            this._cts.Dispose();
        }

        private Int64 Now => this._clock.ElapsedMilliseconds;

        private void WindowLoop()
        {
            var token = this._cts.Token;
            var lastRoot = IntPtr.Zero;
            var candidate = IntPtr.Zero;
            var candidateTicks = 0;
            var lastWindowEvent = Int64.MinValue / 2;
            var wasAtEdge = false;
            var lastEdgeEvent = Int64.MinValue / 2;
            var lastPos = new POINT { X = Int32.MinValue };

            while (!token.WaitHandle.WaitOne(WindowPollMs))
            {
                try
                {
                    if (!Native.GetCursorPos(out var p))
                    {
                        continue;
                    }

                    var moved = p.X != lastPos.X || p.Y != lastPos.Y;
                    lastPos = p;

                    // --- okraj plochy ---
                    var atEdge = IsAtDesktopEdge(p);
                    if (atEdge && !wasAtEdge && this.Now - lastEdgeEvent > EdgeDebounceMs)
                    {
                        lastEdgeEvent = this.Now;
                        this._raise(Reactions.ScreenEdge);
                    }

                    wasAtEdge = atEdge;

                    // --- hranice okna ---
                    if (!moved && candidateTicks == 0)
                    {
                        continue;
                    }

                    var root = Native.GetAncestor(Native.WindowFromPoint(p), Native.GA_ROOT);
                    if (root == IntPtr.Zero || root == lastRoot)
                    {
                        candidateTicks = 0;
                        continue;
                    }

                    // nové okno musí vydržet 2 vzorky – filtruje problikávání popupů/tooltipů
                    if (root != candidate)
                    {
                        candidate = root;
                        candidateTicks = 1;
                        continue;
                    }

                    if (++candidateTicks < 2)
                    {
                        continue;
                    }

                    candidateTicks = 0;
                    var hadPrevious = lastRoot != IntPtr.Zero;
                    lastRoot = root;

                    if (hadPrevious && !IsIgnoredWindow(root) && this.Now - lastWindowEvent > WindowDebounceMs)
                    {
                        lastWindowEvent = this.Now;
                        this._raise(Reactions.WindowBorder);
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"MxHaptics window loop: {ex}");
                }
            }
        }

        private void ControlLoop()
        {
            var token = this._cts.Token;
            String lastKey = null;
            var lastPos = new POINT { X = Int32.MinValue };
            var lastControlEvent = Int64.MinValue / 2;
            using var taskbar = new TaskbarButtons();

            while (!token.WaitHandle.WaitOne(ControlPollMs))
            {
                try
                {
                    if (!Native.GetCursorPos(out var p) || (p.X == lastPos.X && p.Y == lastPos.Y))
                    {
                        continue;
                    }

                    lastPos = p;

                    // při tažení (výběr, drag&drop) prvky ignorujeme
                    if (Native.IsButtonDown())
                    {
                        lastKey = null;
                        continue;
                    }

                    // hlavní lišta Windows 11 je pro MSAA jeden blok, tlačítka bere z UI Automation
                    String key = null;
                    if (!this._taskbarEnabled() || !taskbar.TryGetKey(p, this.Now, out key))
                    {
                        key = FindClickableKey(p);
                    }
                    if (key != null && key != lastKey && this.Now - lastControlEvent > ControlDebounceMs)
                    {
                        lastControlEvent = this.Now;
                        this._raise(Reactions.ControlHover);
                    }

                    lastKey = key;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"MxHaptics control loop: {ex}");
                }
            }
        }

        private static Boolean IsAtDesktopEdge(POINT p)
        {
            // okraj = sousední pixel neleží na žádném monitoru (vnitřní hrany mezi monitory se nepočítají)
            return !OnAnyMonitor(p.X - 1, p.Y) || !OnAnyMonitor(p.X + 1, p.Y)
                || !OnAnyMonitor(p.X, p.Y - 1) || !OnAnyMonitor(p.X, p.Y + 1);
        }

        private static Boolean OnAnyMonitor(Int32 x, Int32 y) =>
            MonitorFromPoint(new POINT { X = x, Y = y }, 0 /* MONITOR_DEFAULTTONULL */) != IntPtr.Zero;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT point, UInt32 flags);

        private static Boolean IsIgnoredWindow(IntPtr hwnd)
        {
            var cls = Native.ClassName(hwnd);
            return cls == "tooltips_class32" || cls == "SysShadow" || cls == "Xaml_WindowedPopupClass";
        }

        /// <summary>Vrátí identitu klikatelného prvku pod kurzorem (role + obdélník), nebo null.</summary>
        private static String FindClickableKey(POINT p)
        {
            IAccessible acc = null;
            try
            {
                if (Native.AccessibleObjectFromPoint(p, out acc, out var child) != 0 || acc == null)
                {
                    return null;
                }

                // jednoduchý potomek (child id != 0)
                if (child is Int32 childId && childId != 0)
                {
                    var key = KeyIfClickable(acc, childId);
                    if (key != null)
                    {
                        return key;
                    }
                }

                // samotný objekt a pár úrovní rodičů (Chrome/Edge vrací text uvnitř odkazu/tlačítka)
                var current = acc;
                for (var depth = 0; depth < 4 && current != null; depth++)
                {
                    var key = KeyIfClickable(current, 0);
                    if (key != null)
                    {
                        if (!ReferenceEquals(current, acc))
                        {
                            Marshal.ReleaseComObject(current);
                        }

                        return key;
                    }

                    var parent = SafeParent(current);
                    if (!ReferenceEquals(current, acc))
                    {
                        Marshal.ReleaseComObject(current);
                    }

                    current = parent;
                }

                if (current != null && !ReferenceEquals(current, acc))
                {
                    Marshal.ReleaseComObject(current);
                }

                return null;
            }
            catch (COMException)
            {
                return null;
            }
            finally
            {
                if (acc != null)
                {
                    Marshal.ReleaseComObject(acc);
                }
            }
        }

        private static IAccessible SafeParent(IAccessible acc)
        {
            try
            {
                return acc.get_accParent() as IAccessible;
            }
            catch (COMException)
            {
                return null;
            }
            catch (InvalidCastException)
            {
                return null;
            }
        }

        private static String KeyIfClickable(IAccessible acc, Int32 childId)
        {
            try
            {
                if (acc.get_accRole(childId) is not Int32 role || Array.IndexOf(ClickableRoles, role) < 0)
                {
                    return null;
                }

                acc.accLocation(out var l, out var t, out var w, out var h, childId);
                return $"{role}:{l}:{t}:{w}:{h}";
            }
            catch (COMException)
            {
                return null;
            }
        }
    }
}
