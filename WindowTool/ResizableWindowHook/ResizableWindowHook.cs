﻿using Hi3Helper.Data;
using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.Native.Enums;
using Hi3Helper.Win32.Native.LibraryImport;
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
        private const int RefreshRateMs = 250;  // Loop refresh rate = 250ms

        public static void StartHook(string            processName,
                                     int?              height,
                                     int?              width,
                                     bool              isNeedResetOnInit   = false,
                                     ILogger?          logger              = null,
                                     string?           checkProcessFromDir = null,
                                     CancellationToken token               = default)
        {
            try
            {
                // Try to get the window property from the process
                WindowProperty targetWindow = GetProcessWindowProperty(processName, checkProcessFromDir, logger, token);

                // Assign the current style and initial pos + size of the window to old variable
                WS_STYLE oldStyle = targetWindow.InitialStyle;
                WindowRect oldPos = targetWindow.InitialPos;

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
                    WS_STYLE curStyle = targetWindow.CurrentStyle;
                    WindowRect curPos = targetWindow.CurrentPos;

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
                            logger?.LogDebug("\r\nCurrent window style has changed!\r\n\tfrom:\t0x{oldStyle:x}\t({oldStyleInBinary})\r\n\tto:\t0x{curStyle:x}\t({curStyleInBinary})", oldStyle, ConverterTool.ToBinaryString((uint)oldStyle), curStyle, ConverterTool.ToBinaryString((uint)curStyle));
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
                                logger?.LogDebug("Moving window to posX: {oldPos} posY: {oldPos} W: {width} H: {height}", oldPos.X, oldPos.Y, width, height);
                                targetWindow.ChangePosition(oldPos.X, oldPos.Y, oldPos.Width, oldPos.Height);
                            }

                            curStyle = targetWindow.CurrentStyle;
                            curPos   = targetWindow.CurrentPos;
                        }

                        // Assign the old style and pos + size variable to the current one.
                        oldStyle = curStyle;
                        oldPos   = curPos;
                    }

                    // Do the delay before running the next loop iteration.
                    Thread.Sleep(RefreshRateMs);
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

        private static bool IsStyleChanged(WS_STYLE oldStyle, WS_STYLE newStyle)
        {
            // Get the mask by OR the WS_MINIMIZE and WS_MAXIMIZE flag
            const WS_STYLE ignoreMinimizeMaximizeStyleMask = WS_STYLE.WS_MINIMIZE | WS_STYLE.WS_MAXIMIZE;

            // Mask the old style and new style to ignore the WS_MINIMIZE and WS_MAXIMIZE flag
            oldStyle &= ~ignoreMinimizeMaximizeStyleMask;
            newStyle &= ~ignoreMinimizeMaximizeStyleMask;

            // Return the masked old and new style and compare the value
            return oldStyle != newStyle;
        }

        private static unsafe WindowProperty GetProcessWindowProperty(string processName, string? checkProcessFromDir, ILogger? logger = null, CancellationToken token = default)
        {
            logger?.LogDebug("Waiting for process handle: {processName}", processName);

            // Do the loop to try getting the process
            while (!token.IsCancellationRequested)
            {
                // Try to get the process detail
                bool isProcessExist = ProcessChecker.IsProcessExist(processName, out int processId, out nint windowHandle, checkProcessFromDir ?? "", true, logger);
                // If the return process is null (not found), then delay and redo the loop
                if (!isProcessExist)
                {
                    Thread.Sleep(RefreshRateMs);
                    continue;
                }

                // If the return process is assigned, then get the initial size + position and style of the window
                WindowRect  initialRect    = new();
                WindowRect* initialRectPtr = (WindowRect*)Unsafe.AsPointer(ref initialRect);

                PInvoke.GetWindowRect(windowHandle, initialRectPtr);
                WS_STYLE initialStyle = PInvoke.GetWindowLong(windowHandle, GWL_INDEX.GWL_STYLE);

                logger?.LogDebug("Got process handle with name: {processName} (PID: {processId}) - (WindowHandle at: 0x{windowHandle:x})", processName, processId, windowHandle);
                logger?.LogDebug("Initial window style enum is: 0x{initialStyle:x}\t({inHex})", initialStyle, ConverterTool.ToBinaryString((uint)initialStyle));

                // Return the window property
                return new WindowProperty(windowHandle, processId, initialStyle, initialRect);
            }

            // If the cancel has been called, then return the empty struct
            return WindowProperty.Empty();
        }
    }
}
