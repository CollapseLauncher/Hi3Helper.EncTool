using Hi3Helper.Data;
using Hi3Helper.Win32.Native;
using Hi3Helper.Win32.Native.Enums;
using Hi3Helper.Win32.Native.Structs;
using System;
using System.Diagnostics;
using System.Linq;

namespace Hi3Helper.EncTool.WindowTool
{
    internal unsafe struct WindowProperty
    {
        public nint hwnd;
        public int procId;
        public WS_STYLE initialStyle;
        public WS_STYLE currentStyle;
        public WindowRect* initialPos;
        public WindowRect* currentPos;
        public bool isEmpty;

        internal byte[] initialPosBuffer;
        internal byte[] currentPosBuffer;

        public static WindowProperty Empty() => new WindowProperty { isEmpty = true };

        public void ToggleBorder(bool isEnable)
        {
            // Toggle the WS_CAPTION and WS_THICKFRAME flag
            const WS_STYLE toggleBorderStyle = WS_STYLE.WS_CAPTION | WS_STYLE.WS_THICKFRAME;
            currentStyle = isEnable ?
                currentStyle | toggleBorderStyle
              : currentStyle & ~toggleBorderStyle;
        }

        public void ToggleResizable(bool isEnable)
        {
            // Toggle the WS_POPUPWINDOW, WS_SIZEBOX and WS_MAXIMIZEBOX flag
            const WS_STYLE toggleResizableStyle = WS_STYLE.WS_POPUPWINDOW | WS_STYLE.WS_SIZEBOX | WS_STYLE.WS_MAXIMIZEBOX;
            currentStyle = isEnable ?
                currentStyle | toggleResizableStyle
              : currentStyle & ~toggleResizableStyle;
        }

        public void ToggleWindowButton(bool isEnable)
        {
            // Toggle the WS_SYSMENU flag
            const WS_STYLE toggleWinButtonStyle = WS_STYLE.WS_SYSMENU;
            currentStyle = isEnable ?
                currentStyle | toggleWinButtonStyle
              : currentStyle & ~(toggleWinButtonStyle);
        }

        public void ChangePosition(int? x = null, int? y = null, int? width = null, int? height = null)
        {
            // Assign a number with the current position if one of the arguments is null
            x ??= currentPos->X;
            y ??= currentPos->Y;
            width ??= currentPos->Width;
            height ??= currentPos->Height;

            // Assign the current value of the currentPos and call the SetWindowPos function
            // ReSharper disable ConstantNullCoalescingCondition
            PInvoke.SetWindowPos(hwnd, 0, currentPos->X = x ?? 0, currentPos->Y = y ?? 0, currentPos->Width = width ?? 0, currentPos->Height = height ?? 0, SWP_FLAGS.SWP_NOZORDER);
            // ReSharper restore ConstantNullCoalescingCondition
        }

        public bool IsWindowBorderlessFullscreen()
        {
            // Get the mask by using WS_SYSMENU Flag
            WS_STYLE hasMask = WS_STYLE.WS_SYSMENU;

            // Remove the WS_SYSMENU Flag by AND the current style and the mask
            // Leaving only the bit representing the WS_SYSMENU flag on the currentStyle.
            // If it has the bit, then it will return all zero. If not, then return 0x8000
            // Or in binary:
            // 1000 0000 0000 0000
            WS_STYLE maskedCurrentStyle = currentStyle & hasMask;

            // Compare the masked style with the WS_SYSMENU mask
            return hasMask != maskedCurrentStyle;
        }

        public void RefreshCurrentStyle() => currentStyle = PInvoke.GetWindowLong(hwnd, GWL_INDEX.GWL_STYLE);
        public void RefreshCurrentPosition() => PInvoke.GetWindowRect(hwnd, currentPos);

        public void ResetStyleToDefault()
        {
            // Reset the current style as it's using the initial one
            Console.Write($"\rReset the window style enum to: 0x{(uint)currentStyle:x8}\t({ConverterTool.ToBinaryString((uint)currentStyle)})");
            currentStyle = initialStyle;
            PInvoke.SetWindowLong(hwnd, GWL_INDEX.GWL_STYLE, currentStyle);
        }

        public void ResetPosToDefault()
        {
            // Reset the current pos + size as it's using the initial one by copying the buffer
            Array.Copy(initialPosBuffer, currentPosBuffer, initialPosBuffer.Length);

            // Then apply the change
            ChangePosition(currentPos->X, currentPos->Y, currentPos->Width, currentPos->Height);
        }

        public void ApplyStyle()
        {
            // Apply the current style with GWL_STYLE flag to SetWindowLongA() native method
            Console.Write($"\rSetting the window style enum to: 0x{(uint)currentStyle:x8}\t({ConverterTool.ToBinaryString((uint)currentStyle)})");
            PInvoke.SetWindowLong(hwnd, GWL_INDEX.GWL_STYLE, currentStyle);
        }

        public bool IsProcessAlive()
        {
            int proc = procId;
#nullable enable
            // Try to get the process based on its matching ID
            Process? process = Process.GetProcesses().Where(x =>
            {
                // If it matches, then return true
                if (x.Id == proc)
                    return true;

                // Otherwise, dispose and return false
                x.Dispose();
                return false;
            }).FirstOrDefault();

            // Dispose the process and return check if it's not null
            process?.Dispose();
            return process != null;
#nullable disable
        }
    }
}
