namespace Loupedeck.MxHapticsPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Tlačítka hlavní lišty Windows 11. Lišta je XAML ostrov: MSAA i UIA ElementFromPoint vrátí jen celou lištu,
    /// ale strom UI Automation (FindAll) tlačítka i s obdélníky vydá. Obdélníky cachujeme a hit-test děláme sami.
    /// Volat jen z jednoho vlákna (vlákno hit-testu), UIA volání do Exploreru můžou při jeho zaseknutí blokovat.
    /// </summary>
    internal sealed class TaskbarButtons : IDisposable
    {
        private const Int64 RefreshMs = 1000;
        private const Int32 UIA_BoundingRectanglePropertyId = 30001;
        private const Int32 UIA_ControlTypePropertyId = 30003;
        private const Int32 UIA_ButtonControlTypeId = 50000;
        private const Int32 TreeScope_Descendants = 4;

        private IUIAutomation _uia;
        private IUIAutomationCondition _buttonCondition;
        private IntPtr _cachedBar;
        private Int64 _cachedAt;
        private Rect[] _rects = Array.Empty<Rect>();

        internal readonly struct Rect
        {
            public readonly Int32 Left, Top, Width, Height;

            public Rect(Int32 left, Int32 top, Int32 width, Int32 height)
            {
                this.Left = left;
                this.Top = top;
                this.Width = width;
                this.Height = height;
            }

            public Boolean Contains(POINT p) =>
                p.X >= this.Left && p.X < this.Left + this.Width && p.Y >= this.Top && p.Y < this.Top + this.Height;

            public override String ToString() => $"{this.Left}:{this.Top}:{this.Width}:{this.Height}";
        }

        public static Boolean IsTaskbar(IntPtr root)
        {
            var cls = Native.ClassName(root);
            return cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd";
        }

        /// <summary>
        /// Když je kurzor nad hlavní lištou, vrátí true a v <paramref name="key"/> identitu tlačítka pod ním (nebo null).
        /// Mimo lištu vrátí false a hit-test má převzít MSAA.
        /// </summary>
        public Boolean TryGetKey(POINT cursor, Int64 now, out String key)
        {
            key = null;
            var root = Native.GetAncestor(Native.WindowFromPoint(cursor), Native.GA_ROOT);
            if (root == IntPtr.Zero || !IsTaskbar(root))
            {
                return false;
            }

            if (root != this._cachedBar || now - this._cachedAt > RefreshMs)
            {
                this._rects = this.LoadButtonRects(root);
                this._cachedBar = root;
                this._cachedAt = now;
            }

            // UIA vrací fyzické pixely, GetCursorPos může být u DPI-unaware procesu virtualizovaný
            if (Native.GetPhysicalCursorPos(out var physical))
            {
                key = KeyAt(this._rects, physical);
            }

            return true;
        }

        internal static String KeyAt(Rect[] rects, POINT physical)
        {
            foreach (var r in rects)
            {
                if (r.Contains(physical))
                {
                    return "taskbar:" + r;
                }
            }

            return null;
        }

        internal Rect[] LoadButtonRects(IntPtr taskbar)
        {
            IUIAutomationElement bar = null;
            IUIAutomationElementArray found = null;
            try
            {
                if (this._uia == null)
                {
                    this._uia = (IUIAutomation)Activator.CreateInstance(
                        Type.GetTypeFromCLSID(new Guid("FF48DBA4-60EF-4201-AA87-54103EEF594E"), throwOnError: true));
                    this._buttonCondition = this._uia.CreatePropertyCondition(UIA_ControlTypePropertyId, UIA_ButtonControlTypeId);
                }

                bar = this._uia.ElementFromHandle(taskbar);
                found = bar.FindAll(TreeScope_Descendants, this._buttonCondition);

                var count = found.get_Length();
                var rects = new List<Rect>(count);
                for (var i = 0; i < count; i++)
                {
                    var element = found.GetElement(i);
                    try
                    {
                        if (element.GetCurrentPropertyValue(UIA_BoundingRectanglePropertyId) is Double[] { Length: 4 } b && b[2] > 0 && b[3] > 0)
                        {
                            rects.Add(new Rect((Int32)b[0], (Int32)b[1], (Int32)b[2], (Int32)b[3]));
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(element);
                    }
                }

                return rects.ToArray();
            }
            catch (Exception ex) when (ex is COMException or InvalidCastException)
            {
                Trace.WriteLine($"MxHaptics: tlačítka lišty nejdou načíst: {ex.Message}");
                return Array.Empty<Rect>();
            }
            finally
            {
                if (found != null)
                {
                    Marshal.ReleaseComObject(found);
                }

                if (bar != null)
                {
                    Marshal.ReleaseComObject(bar);
                }
            }
        }

        public void Dispose()
        {
            if (this._buttonCondition != null)
            {
                Marshal.ReleaseComObject(this._buttonCondition);
                this._buttonCondition = null;
            }

            if (this._uia != null)
            {
                Marshal.ReleaseComObject(this._uia);
                this._uia = null;
            }
        }
    }

    // UI Automation (UIAutomationClient.h). Jen prefix vtable; nepoužité metody drží místo (pořadí musí sedět).
    [ComImport]
    [Guid("30CBE57D-D9D0-452A-AB13-7AC5AC4825EE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IUIAutomation
    {
        void CompareElements();

        void CompareRuntimeIds();

        void GetRootElement();

        IUIAutomationElement ElementFromHandle(IntPtr hwnd);

        void ElementFromPoint();

        void GetFocusedElement();

        void GetRootElementBuildCache();

        void ElementFromHandleBuildCache();

        void ElementFromPointBuildCache();

        void GetFocusedElementBuildCache();

        void CreateTreeWalker();

        void get_ControlViewWalker();

        void get_ContentViewWalker();

        void get_RawViewWalker();

        void get_RawViewCondition();

        void get_ControlViewCondition();

        void get_ContentViewCondition();

        void CreateCacheRequest();

        void CreateTrueCondition();

        void CreateFalseCondition();

        IUIAutomationCondition CreatePropertyCondition(Int32 propertyId, [MarshalAs(UnmanagedType.Struct)] Object value);
    }

    [ComImport]
    [Guid("352FFBA8-0973-437C-A61F-F64CAFD81DF9")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IUIAutomationCondition
    {
    }

    [ComImport]
    [Guid("D22108AA-8AC5-49A5-837B-37BBB3D7591E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IUIAutomationElement
    {
        void SetFocus();

        void GetRuntimeId();

        void FindFirst();

        IUIAutomationElementArray FindAll(Int32 scope, IUIAutomationCondition condition);

        void FindFirstBuildCache();

        void FindAllBuildCache();

        void BuildUpdatedCache();

        [return: MarshalAs(UnmanagedType.Struct)]
        Object GetCurrentPropertyValue(Int32 propertyId);
    }

    [ComImport]
    [Guid("14314595-B4BC-4055-95F2-58F2E42C9855")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IUIAutomationElementArray
    {
        Int32 get_Length();

        IUIAutomationElement GetElement(Int32 index);
    }
}
