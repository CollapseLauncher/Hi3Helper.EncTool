using Hi3Helper.Data;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Hi3Helper.EncTool.WindowTool
{
    internal struct WindowProperty
    {
        public nint             hwnd;
        public int              procId;
        public PInvoke.WS_STYLE initialStyle;
        public PInvoke.WS_STYLE currentStyle;
        public WindowRect       initialPos;
        public WindowRect       currentPos;
        public bool             isEmpty;

        public static WindowProperty Empty() => new WindowProperty { isEmpty = true };

        public void ToggleBorder(bool isEnable)
        {
            // Toggle the WS_CAPTION and WS_THICKFRAME flag
            const PInvoke.WS_STYLE toggleBorderStyle = PInvoke.WS_STYLE.WS_CAPTION | PInvoke.WS_STYLE.WS_THICKFRAME;
            currentStyle = isEnable ?
                currentStyle | toggleBorderStyle
              : currentStyle & ~toggleBorderStyle;
        }

        public void ToggleResizable(bool isEnable)
        {
            // Toggle the WS_POPUPWINDOW, WS_SIZEBOX and WS_MAXIMIZEBOX flag
            const PInvoke.WS_STYLE toggleResizableStyle = PInvoke.WS_STYLE.WS_POPUPWINDOW | PInvoke.WS_STYLE.WS_SIZEBOX | PInvoke.WS_STYLE.WS_MAXIMIZEBOX;
            currentStyle = isEnable ?
                currentStyle | toggleResizableStyle
              : currentStyle & ~toggleResizableStyle;
        }

        public void ToggleWindowButton(bool isEnable)
        {
            // Toggle the WS_SYSMENU flag
            const PInvoke.WS_STYLE toggleWinButtonStyle = PInvoke.WS_STYLE.WS_SYSMENU;
            currentStyle = isEnable ?
                currentStyle | toggleWinButtonStyle
              : currentStyle & ~(toggleWinButtonStyle);
        }

        public void ChangePosition(int? x = null, int? y = null, int? width = null, int? height = null)
        {
            // Assign a number with the current position if one of the arguments is null
            x ??= currentPos.X;
            y ??= currentPos.Y;
            width ??= currentPos.Width;
            height ??= currentPos.Height;

            // Assign the current value of the currentPos and call the SetWindowPos function
            // ReSharper disable ConstantNullCoalescingCondition
            PInvoke.SetWindowPos(hwnd, 0, currentPos.X = x ?? 0, currentPos.Y = y ?? 0, currentPos.Width = width ?? 0, currentPos.Height = height ?? 0, PInvoke.SWP_FLAGS.SWP_NOZORDER);
            // ReSharper restore ConstantNullCoalescingCondition
        }

        public bool IsWindowBorderlessFullscreen()
        {
            // Get the mask by using WS_SYSMENU Flag
            PInvoke.WS_STYLE hasMask = PInvoke.WS_STYLE.WS_SYSMENU;

            // Remove the WS_SYSMENU Flag by AND the current style and the mask
            // Leaving only the bit representing the WS_SYSMENU flag on the currentStyle.
            // If it has the bit, then it will return all zero. If not, then return 0x8000
            // Or in binary:
            // 1000 0000 0000 0000
            PInvoke.WS_STYLE maskedCurrentStyle = currentStyle & hasMask;

            // Compare the masked style with the WS_SYSMENU mask
            return hasMask != maskedCurrentStyle;
        }

        public void RefreshCurrentStyle() => currentStyle = PInvoke.GetWindowLongA(hwnd, PInvoke.GWL_INDEX.GWL_STYLE);
        public void RefreshCurrentPosition() => PInvoke.GetWindowRect(hwnd, ref currentPos);

        public void ResetStyleToDefault()
        {
            // Reset the current style as it's using the initial one
            Console.Write($"\rReset the window style enum to: 0x{(uint)currentStyle:x8}\t({ConverterTool.ToBinaryString((uint)currentStyle)})");
            currentStyle = initialStyle;
            PInvoke.SetWindowLongA(hwnd, PInvoke.GWL_INDEX.GWL_STYLE, currentStyle);
        }

        public void ResetPosToDefault()
        {
            // Reset the current pos + size as it's using the initial one
            currentPos = new WindowRect
            {
                X = initialPos.X,
                Y = initialPos.Y,
                Width = initialPos.Width,
                Height = initialPos.Height
            };

            // Then apply the change
            ChangePosition(currentPos.X, currentPos.Y, currentPos.Width, currentPos.Height);
        }

        public void ApplyStyle()
        {
            // Apply the current style with GWL_STYLE flag to SetWindowLongA() native method
            Console.Write($"\rSetting the window style enum to: 0x{(uint)currentStyle:x8}\t({ConverterTool.ToBinaryString((uint)currentStyle)})");
            PInvoke.SetWindowLongA(hwnd, PInvoke.GWL_INDEX.GWL_STYLE, currentStyle);
        }

        public async ValueTask<bool> IsProcessAlive()
        {
            int proc = procId;
            // Run the check as a fake async routine
            return await Task.Run(() =>
            {
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
            });
        }
    }
}
