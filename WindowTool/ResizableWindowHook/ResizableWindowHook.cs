using Hi3Helper.Data;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hi3Helper.EncTool.WindowTool
{
    public class ResizableWindowHook
    {
        const int refreshRateMs = 250;  // Loop refresh rate = 250ms

        public async Task StartHook(string processName, CancellationToken token = default, bool isNeedResetOnInit = false)
        {
            // Initialize the empty window property struct
            WindowProperty targetWindow = WindowProperty.Empty();
            try
            {
                // Try get the window property from the process
                targetWindow = await GetProcessWindowProperty(processName, token);

                // Assign the current style and initial pos + size of the window to old variable
                PInvoke.WS_STYLE oldStyle = targetWindow.initialStyle;
                WindowRect oldPos = targetWindow.initialPos;

                // Always set the window border and resizable flag to true
                targetWindow.ToggleBorder(true);
                targetWindow.ToggleResizable(true);
                targetWindow.ApplyStyle();

                // Do loop for tracking the window style and pos + size changes while
                // the process is still alive
                while (await targetWindow.IsProcessAlive())
                {
                    // Refresh the current style and pos + size of the window
                    targetWindow.RefreshCurrentStyle();
                    targetWindow.RefreshCurrentPosition();

                    // Get the current style and pos + size of the window
                    PInvoke.WS_STYLE curStyle = targetWindow.currentStyle;
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
                            Console.WriteLine($"\r\nCurrent window style has changed!\r\n\tfrom:\t0x{(uint)oldStyle:x8}\t({ConverterTool.ToBinaryString((uint)oldStyle)})\r\n\tto:\t0x{(uint)curStyle:x8}\t({ConverterTool.ToBinaryString((uint)curStyle)})");
                            Console.WriteLine("Resetting...");

                            // Reset the window style and pos + size to the old state
                            targetWindow.ToggleBorder(true);
                            targetWindow.ToggleResizable(true);
                            targetWindow.ApplyStyle();
                            targetWindow.ChangePosition(oldPos.X, oldPos.Y, oldPos.Width, oldPos.Height);

                            // If reset to the initial style is required and the pos + size is changed,
                            // then reset the pos + size to its initial style
                            if (isNeedResetOnInit && !curPos.Equals(oldPos))
                            {
                                isNeedResetOnInit = false;
                                targetWindow.ResetPosToDefault();
                            }

                            // Refresh the current style and pos + size of the window
                            targetWindow.RefreshCurrentStyle();
                            targetWindow.RefreshCurrentPosition();

                            curStyle = targetWindow.currentStyle;
                            curPos = targetWindow.currentPos;
                        }

                        // Assign the old style and pos + size variable to the current one.
                        oldStyle = curStyle;
                        oldPos = curPos;
                    }

                    // Do the delay before running the next loop iteration.
                    await Task.Delay(refreshRateMs, token);
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

        private bool IsStyleChanged(PInvoke.WS_STYLE oldStyle, PInvoke.WS_STYLE newStyle)
        {
            // Get the mask by OR the WS_MINIMIZE and WS_MAXIMIZE flag
            const PInvoke.WS_STYLE ignoreMinimizeMaximizeStyleMask = PInvoke.WS_STYLE.WS_MINIMIZE | PInvoke.WS_STYLE.WS_MAXIMIZE;

            // Mask the old style and new style to ignore the WS_MINIMIZE and WS_MAXIMIZE flag
            oldStyle &= ~ignoreMinimizeMaximizeStyleMask;
            newStyle &= ~ignoreMinimizeMaximizeStyleMask;

            // Return the masked old and new style and compare the value
            return oldStyle != newStyle;
        }

        private async ValueTask<WindowProperty> GetProcessWindowProperty(string processName, CancellationToken token)
        {
            Console.WriteLine($"Waiting for process handle: {processName}");
            // Do the loop to try getting the process
            while (!token.IsCancellationRequested)
            {
                // Run the LINQ as a fake async routine
#nullable enable
                using (Process? returnProcess = await Task.Run(() =>
                {
                    // Get the process list
                    Process[] processes = Process.GetProcessesByName(processName);
                    Process? _ret = processes.Where(x =>
                    {
                        // If the HWND of the process is not null (!Zero), then return true
                        if (x.MainWindowHandle != nint.Zero)
                            return true;

                        // Otherwise, dispose the process instance and return false
                        x.Dispose();
                        return false;
                    }).FirstOrDefault(); // Select the first or default (null)

                    // Return the process
                    return _ret;
                }))
#nullable disable
                {
                    // If the return process is null (not found), then delay and redo the loop
                    if (returnProcess == null)
                    {
                        await Task.Delay(refreshRateMs, token);
                        continue;
                    }

                    // If the return process is assigned, then get the initial size + position and style of the window
                    WindowRect initialRect = new WindowRect();
                    PInvoke.GetWindowRect(returnProcess.MainWindowHandle, ref initialRect);
                    PInvoke.WS_STYLE initialStyle = PInvoke.GetWindowLongA(returnProcess.MainWindowHandle, PInvoke.GWL_INDEX.GWL_STYLE);

                    // Assign the current position + size of the window
                    WindowRect currentRect = new WindowRect
                    {
                        X = initialRect.X,
                        Y = initialRect.Y,
                        Width = initialRect.Width,
                        Height = initialRect.Height,
                    };

                    Console.WriteLine($"Got process handle with name: {returnProcess.ProcessName} (PID: {returnProcess.Id}) - (HWND at: 0x{returnProcess.MainWindowHandle:x})");
                    Console.WriteLine($"Initial window style enum is: 0x{(uint)initialStyle:x8}\t({ConverterTool.ToBinaryString((uint)initialStyle)})");

                    // Return the window property
                    return new WindowProperty
                    {
                        hwnd = returnProcess.MainWindowHandle,
                        procId = returnProcess.Id,
                        initialStyle = initialStyle,
                        currentStyle = initialStyle,
                        initialPos = initialRect,
                        currentPos = currentRect
                    };
                }
            }

            // If the cancel has been called, then return the empty struct
            return WindowProperty.Empty();
        }
    }
}
