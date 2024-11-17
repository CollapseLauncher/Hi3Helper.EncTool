using Hi3Helper.Data;
using Hi3Helper.Win32.Native;
using Hi3Helper.Win32.Native.Enums;
using Hi3Helper.Win32.Native.Structs;
using Microsoft.Extensions.Logging;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace Hi3Helper.EncTool.WindowTool
{
    public class ResizableWindowHook
    {
        const int refreshRateMs = 250;  // Loop refresh rate = 250ms

        public unsafe void StartHook(string processName, int? height, int? width,
                                    CancellationToken token = default,
                                    bool isNeedResetOnInit = false,
                                    ILogger? logger = null,
                                    string? checkProcessFromDir = null)
        {
            try
            {
                // Try get the window property from the process
                WindowProperty targetWindow = GetProcessWindowProperty(processName, checkProcessFromDir, token, logger);

                // Assign the current style and initial pos + size of the window to old variable
                WS_STYLE oldStyle = targetWindow.initialStyle;
                WindowRect oldPos = targetWindow.initialPos;

                // Always set the window border and resizable flag to true
                targetWindow.ToggleBorder(true);
                targetWindow.ToggleResizable(true);
                targetWindow.ApplyStyle();

                // Do loop for tracking the window style and pos + size changes while
                // the process is still alive
                while (targetWindow.IsProcessAlive())
                {
                    // Refresh the current style and pos + size of the window
                    targetWindow.RefreshCurrentStyle();
                    targetWindow.RefreshCurrentPosition();

                    // Get the current style and pos + size of the window
                    WS_STYLE curStyle = targetWindow.currentStyle;
                    WindowRect curPos = targetWindow.currentPos;

                    // Check if the window is in a borderless fullscreen (by checking if it doesn't have SYS_MENU flag)
                    // and the style is changed, then reset the style and pos + size to the last state.
                    // 
                    // This unattended changes sometimes occurred on Honkai Impact 3rd Scene/Level/Menu changes
                    // and Honkai: Star Rail first start-up.
                    bool isFullscreen = targetWindow.IsWindowBorderlessFullscreen();
                    if (!isFullscreen)
                    {
                        if (IsStyleChanged(oldStyle, curStyle))
                        {
                            logger?.LogDebug($"\r\nCurrent window style has changed!\r\n\tfrom:\t0x{(uint)oldStyle:x8}\t({ConverterTool.ToBinaryString((uint)oldStyle)})\r\n\tto:\t0x{(uint)curStyle:x8}\t({ConverterTool.ToBinaryString((uint)curStyle)})");
                            logger?.LogDebug("Resetting...");

                            // Reset the window style and pos + size to the old state
                            targetWindow.ToggleBorder(true);
                            targetWindow.ToggleResizable(true);
                            targetWindow.ApplyStyle();
                            targetWindow.ChangePosition(oldPos.X, oldPos.Y, oldPos.Width, oldPos.Height);

                            // If reset to the initial style is required and the pos + size is changed,
                            // then reset the pos + size to its initial style
                            if (isNeedResetOnInit && !curPos.Equals(ref oldPos))
                            {
                                isNeedResetOnInit = false;
                                targetWindow.ResetPosToDefault();
                            }

                            // Refresh the current style and pos + size of the window
                            targetWindow.RefreshCurrentStyle();
                            targetWindow.RefreshCurrentPosition();
                            if (height != null && width != null)
                            {
                                logger?.LogDebug($"Moving window to posX: {oldPos.X} posY: {oldPos.Y} W: {width} H: {height}");
                                targetWindow.ChangePosition(oldPos.X, oldPos.Y, oldPos.Width, oldPos.Height);
                            }

                            curStyle = targetWindow.currentStyle;
                            curPos = targetWindow.currentPos;
                        }

                        // Assign the old style and pos + size variable to the current one.
                        oldStyle = curStyle;
                        oldPos = curPos;
                    }

                    // Do the delay before running the next loop iteration.
                    Thread.Sleep(refreshRateMs);
                }
            }
            catch (Exception e)
            {
                Type exceptionType = e.GetType();
                // If the exception is not a cancellation exception, then skip
                if (exceptionType == typeof(OperationCanceledException)
                 || exceptionType == typeof(TaskCanceledException))
                    return;

                // Otherwise, YEET!
                throw;
            }
        }

        private bool IsStyleChanged(WS_STYLE oldStyle, WS_STYLE newStyle)
        {
            // Get the mask by OR the WS_MINIMIZE and WS_MAXIMIZE flag
            const WS_STYLE ignoreMinimizeMaximizeStyleMask = WS_STYLE.WS_MINIMIZE | WS_STYLE.WS_MAXIMIZE;

            // Mask the old style and new style to ignore the WS_MINIMIZE and WS_MAXIMIZE flag
            oldStyle &= ~ignoreMinimizeMaximizeStyleMask;
            newStyle &= ~ignoreMinimizeMaximizeStyleMask;

            // Return the masked old and new style and compare the value
            return oldStyle != newStyle;
        }

        private unsafe WindowProperty GetProcessWindowProperty(string processName, string? checkProcessFromDir, CancellationToken token, ILogger? logger)
        {
            logger?.LogDebug($"Waiting for process handle: {processName}");

            // Do the loop to try getting the process
            while (!token.IsCancellationRequested)
            {
                // Try get the process detail
                bool isProcessExist = PInvoke.IsProcessExist(processName, out int processId, out nint hwnd, checkProcessFromDir ?? "", true, logger);
                // If the return process is null (not found), then delay and redo the loop
                if (!isProcessExist)
                {
                    Thread.Sleep(refreshRateMs);
                    continue;
                }

                // If the return process is assigned, then get the initial size + position and style of the window
                WindowRect initialRect = new WindowRect();
                WindowRect* initialRectPtr = (WindowRect*)Unsafe.AsPointer(ref initialRect);

                PInvoke.GetWindowRect(hwnd, initialRectPtr);
                WS_STYLE initialStyle = PInvoke.GetWindowLong(hwnd, GWL_INDEX.GWL_STYLE);

                logger?.LogDebug($"Got process handle with name: {processName} (PID: {processId}) - (HWND at: 0x{hwnd:x})");
                logger?.LogDebug($"Initial window style enum is: 0x{(uint)initialStyle:x8}\t({ConverterTool.ToBinaryString((uint)initialStyle)})");

                // Return the window property
                return new WindowProperty(hwnd, processId, initialStyle, initialRect);
            }

            // If the cancel has been called, then return the empty struct
            return WindowProperty.Empty();
        }
    }
}
