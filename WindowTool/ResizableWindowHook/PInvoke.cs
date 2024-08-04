using System.Runtime.InteropServices;

namespace Hi3Helper.EncTool.WindowTool
{
    internal static class PInvoke
    {
        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern WS_STYLE GetWindowLongA(nint hWnd, GWL_INDEX nIndex);

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern int SetWindowLongA(nint hWnd, GWL_INDEX nIndex, WS_STYLE dwNewLong);

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern bool GetWindowRect(nint hwnd, ref WindowRect rectangle);

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern nint SetWindowPos(nint hWnd, int hWndInsertAfter, int x, int Y, int cx, int cy, SWP_FLAGS wFlags);

        internal enum GWL_INDEX : int
        {
            GWLP_WNDPROC = -4,
            GWLP_HINSTANCE = -6,
            GWLP_ID = -12,
            GWL_STYLE = -16,
            GWL_EXSTYLE = -20,
            GWLP_USERDATA = -21
        }

        internal enum WS_STYLE : uint
        {
            WS_BORDER = 0x00800000,
            WS_CAPTION = 0x00C00000,
            WS_CHILD = 0x40000000,
            WS_CHILDWINDOW = 0x40000000,
            WS_CLIPCHILDREN = 0x02000000,
            WS_CLIPSIBLINGS = 0x04000000,
            WS_DISABLED = 0x08000000,
            WS_DLGFRAME = 0x00400000,
            WS_GROUP = 0x00020000,
            WS_HSCROLL = 0x00100000,
            WS_ICONIC = 0x20000000,
            WS_MAXIMIZE = 0x01000000,
            WS_MAXIMIZEBOX = 0x00010000,
            WS_MINIMIZE = 0x20000000,
            WS_MINIMIZEBOX = 0x00020000,
            WS_OVERLAPPED = 0x00000000,
            WS_OVERLAPPEDWINDOW = (WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX),
            WS_POPUP = 0x80000000,
            WS_POPUPWINDOW = (WS_POPUP | WS_BORDER | WS_SYSMENU),
            WS_SIZEBOX = 0x00040000,
            WS_SYSMENU = 0x00080000,
            WS_TABSTOP = 0x00010000,
            WS_THICKFRAME = 0x00040000,
            WS_TILED = 0x00000000,
            WS_TILEDWINDOW = (WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX),
            WS_VISIBLE = 0x10000000,
            WS_VSCROLL = 0x00200000
        }

        internal enum SWP_FLAGS : uint
        {
            SWP_DRAWFRAME = 0x0020,
            SWP_FRAMECHANGED = 0x0020,
            SWP_HIDEWINDOW = 0x0080,
            SWP_NOACTIVATE = 0x0010,
            SWP_NOCOPYBITS = 0x0100,
            SWP_NOMOVE = 0x0002,
            SWP_NOOWNERZORDER = 0x0200,
            SWP_NOREDRAW = 0x0008,
            SWP_NOREPOSITION = 0x0200,
            SWP_NOSENDCHANGING = 0x0400,
            SWP_NOSIZE = 0x0001,
            SWP_NOZORDER = 0x0004,
            SWP_SHOWWINDOW = 0x0040
        }
    }
}
