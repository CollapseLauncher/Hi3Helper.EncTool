using Hi3Helper.Data;
using Hi3Helper.Win32.Native.Enums;
using Hi3Helper.Win32.Native.LibraryImport;
using Hi3Helper.Win32.Native.ManagedTools;
using Hi3Helper.Win32.Native.Structs;
using System;
using System.Runtime.CompilerServices;
// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo
// ReSharper disable UnusedMember.Global

namespace Hi3Helper.EncTool.WindowTool
{
    internal unsafe struct WindowProperty
    {
        public nint       Hwnd;
        public int        ProcId;
        public WS_STYLE   InitialStyle;
        public WS_STYLE   CurrentStyle;
        public WindowRect InitialPos;
        public WindowRect CurrentPos;
        public bool       IsEmpty;

        public WindowProperty()
        {
            IsEmpty = true;
        }

        public WindowProperty(nint hwndFrom, int procIdFrom, WS_STYLE initialStyleFrom, WindowRect windowRectFrom)
        {
            if (hwndFrom == IntPtr.Zero || procIdFrom == 0)
                return;

            Hwnd         = hwndFrom;
            ProcId       = procIdFrom;
            InitialStyle = initialStyleFrom;
            CurrentStyle = initialStyleFrom;
            InitialPos   = windowRectFrom;
            CurrentPos   = windowRectFrom;
            IsEmpty      = false;
        }

        public static WindowProperty Empty() => new();

        public void ToggleBorder(bool isEnable)
        {
            // Toggle the WS_CAPTION and WS_THICKFRAME flag
            const WS_STYLE toggleBorderStyle = WS_STYLE.WS_CAPTION | WS_STYLE.WS_THICKFRAME;
            CurrentStyle = isEnable ?
                CurrentStyle | toggleBorderStyle
              : CurrentStyle & ~toggleBorderStyle;
        }

        public void ToggleResizable(bool isEnable)
        {
            // Toggle the WS_POPUPWINDOW, WS_SIZEBOX and WS_MAXIMIZEBOX flag
            const WS_STYLE toggleResizableStyle = WS_STYLE.WS_POPUPWINDOW | WS_STYLE.WS_SIZEBOX | WS_STYLE.WS_MAXIMIZEBOX;
            CurrentStyle = isEnable ?
                CurrentStyle | toggleResizableStyle
              : CurrentStyle & ~toggleResizableStyle;
        }

        public void ToggleWindowButton(bool isEnable)
        {
            // Toggle the WS_SYSMENU flag
            const WS_STYLE toggleWinButtonStyle = WS_STYLE.WS_SYSMENU;
            CurrentStyle = isEnable ?
                CurrentStyle | toggleWinButtonStyle
              : CurrentStyle & ~toggleWinButtonStyle;
        }

        public void ChangePosition(int? x = null, int? y = null, int? width = null, int? height = null)
        {
            // Assign a number with the current position if one of the arguments is null
            x ??= CurrentPos.X;
            y ??= CurrentPos.Y;
            width ??= CurrentPos.Width;
            height ??= CurrentPos.Height;

            // Assign the current value of the currentPos and call the SetWindowPos function
            // ReSharper disable ConstantNullCoalescingCondition
            PInvoke.SetWindowPos(Hwnd, 0, CurrentPos.X = x ?? 0, CurrentPos.Y = y ?? 0, CurrentPos.Width = width ?? 0, CurrentPos.Height = height ?? 0, SWP_FLAGS.SWP_NOZORDER);
            // ReSharper restore ConstantNullCoalescingCondition
        }

        public bool IsWindowBorderlessFullscreen()
        {
            // Get the mask by using WS_SYSMENU Flag
            const WS_STYLE hasMask = WS_STYLE.WS_SYSMENU;

            // Remove the WS_SYSMENU Flag by AND the current style and the mask
            // Leaving only the bit representing the WS_SYSMENU flag on the currentStyle.
            // If it has the bit, then it will return all zero. If not, then return 0x8000
            // Or in binary:
            // 1000 0000 0000 0000
            WS_STYLE maskedCurrentStyle = CurrentStyle & hasMask;

            // Compare the masked style with the WS_SYSMENU mask
            return hasMask != maskedCurrentStyle;
        }

        public void RefreshCurrentStyle() => CurrentStyle = PInvoke.GetWindowLong(Hwnd, GWL_INDEX.GWL_STYLE);

        public void RefreshCurrentPosition() => PInvoke.GetWindowRect(Hwnd, (WindowRect*)Unsafe.AsPointer(ref CurrentPos));

        public void ResetStyleToDefault()
        {
            // Reset the current style as it's using the initial one
            Console.Write($"\rReset the window style enum to: 0x{(uint)CurrentStyle:x8}\t({ConverterTool.ToBinaryString((uint)CurrentStyle)})");
            CurrentStyle = InitialStyle;
            PInvoke.SetWindowLong(Hwnd, GWL_INDEX.GWL_STYLE, CurrentStyle);
        }

        public void ResetPosToDefault()
        {
            // Reset the current pos + size as it's using the initial one
            CurrentPos = InitialPos;

            // Then apply the change
            ChangePosition(CurrentPos.X, CurrentPos.Y, CurrentPos.Width, CurrentPos.Height);
        }

        public void ApplyStyle()
        {
            // Apply the current style with GWL_STYLE flag to SetWindowLongA() native method
            Console.Write($"\rSetting the window style enum to: 0x{(uint)CurrentStyle:x8}\t({ConverterTool.ToBinaryString((uint)CurrentStyle)})");
            PInvoke.SetWindowLong(Hwnd, GWL_INDEX.GWL_STYLE, CurrentStyle);
        }

        public bool IsProcessAlive() => ProcessChecker.IsProcessExist(ProcId);
    }
}
