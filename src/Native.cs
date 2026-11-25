namespace Loupedeck.MxHapticsPlugin
{
    using System;
    using System.Runtime.InteropServices;
    using System.Text;

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public Int32 X;
        public Int32 Y;

        public override String ToString() => $"{this.X},{this.Y}";
    }

    internal static class Native
    {
        public const UInt32 GA_ROOT = 2;

        public const Int32 SM_XVIRTUALSCREEN = 76;
        public const Int32 SM_YVIRTUALSCREEN = 77;
        public const Int32 SM_CXVIRTUALSCREEN = 78;
        public const Int32 SM_CYVIRTUALSCREEN = 79;

        public const Int32 VK_LBUTTON = 0x01;
        public const Int32 VK_RBUTTON = 0x02;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern Boolean GetCursorPos(out POINT point);

        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hwnd, UInt32 flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern Int32 GetClassName(IntPtr hwnd, StringBuilder className, Int32 maxCount);

        [DllImport("user32.dll")]
        public static extern Int32 GetSystemMetrics(Int32 index);

        [DllImport("user32.dll")]
        public static extern Int16 GetAsyncKeyState(Int32 key);

        [DllImport("oleacc.dll")]
        public static extern Int32 AccessibleObjectFromPoint(
            POINT point,
            [MarshalAs(UnmanagedType.Interface)] out IAccessible accessible,
            [MarshalAs(UnmanagedType.Struct)] out Object child);

        public static String ClassName(IntPtr hwnd)
        {
            var sb = new StringBuilder(128);
            return GetClassName(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : String.Empty;
        }

        public static Boolean IsButtonDown() =>
            (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0 || (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;
    }

    // MSAA IAccessible – jen prefix vtable, který potřebujeme (pořadí musí odpovídat oleacc.idl).
    [ComImport]
    [Guid("618736E0-3C3D-11CF-810C-00AA00389B71")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    internal interface IAccessible
    {
        [return: MarshalAs(UnmanagedType.IDispatch)]
        Object get_accParent();

        Int32 get_accChildCount();

        [return: MarshalAs(UnmanagedType.IDispatch)]
        Object get_accChild([MarshalAs(UnmanagedType.Struct)] Object child);

        [return: MarshalAs(UnmanagedType.BStr)]
        String get_accName([MarshalAs(UnmanagedType.Struct)] Object child);

        [return: MarshalAs(UnmanagedType.BStr)]
        String get_accValue([MarshalAs(UnmanagedType.Struct)] Object child);

        [return: MarshalAs(UnmanagedType.BStr)]
        String get_accDescription([MarshalAs(UnmanagedType.Struct)] Object child);

        [return: MarshalAs(UnmanagedType.Struct)]
        Object get_accRole([MarshalAs(UnmanagedType.Struct)] Object child);

        [return: MarshalAs(UnmanagedType.Struct)]
        Object get_accState([MarshalAs(UnmanagedType.Struct)] Object child);

        [return: MarshalAs(UnmanagedType.BStr)]
        String get_accHelp([MarshalAs(UnmanagedType.Struct)] Object child);

        Int32 get_accHelpTopic([MarshalAs(UnmanagedType.BStr)] out String helpFile, [MarshalAs(UnmanagedType.Struct)] Object child);

        [return: MarshalAs(UnmanagedType.BStr)]
        String get_accKeyboardShortcut([MarshalAs(UnmanagedType.Struct)] Object child);

        [return: MarshalAs(UnmanagedType.Struct)]
        Object get_accFocus();

        [return: MarshalAs(UnmanagedType.Struct)]
        Object get_accSelection();

        [return: MarshalAs(UnmanagedType.BStr)]
        String get_accDefaultAction([MarshalAs(UnmanagedType.Struct)] Object child);

        void accSelect(Int32 flags, [MarshalAs(UnmanagedType.Struct)] Object child);

        void accLocation(out Int32 left, out Int32 top, out Int32 width, out Int32 height, [MarshalAs(UnmanagedType.Struct)] Object child);
    }
}
