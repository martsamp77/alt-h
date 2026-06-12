using System.Runtime.InteropServices;

namespace AltHMinimize;

internal enum ButtonAction
{
    Off = 0,
    Minimize = 1,
    CloseWindow = 2,
    CtrlW = 3,
    MediaPlayPause = 4,
}

/// <summary>
/// Executes the actions a mouse button or hotkey can be bound to. Window-targeting actions
/// resolve the foreground window through <see cref="WindowFilter"/> so shell surfaces and
/// this app's own windows are never touched.
/// </summary>
internal static class Actions
{
    public static void Execute(ButtonAction action)
    {
        switch (action)
        {
            case ButtonAction.Minimize:
                MinimizeForegroundWindow();
                break;
            case ButtonAction.CloseWindow:
                CloseForegroundWindow();
                break;
            case ButtonAction.CtrlW:
                SendCtrlW();
                break;
            case ButtonAction.MediaPlayPause:
                SendMediaPlayPause();
                break;
        }
    }

    public static void MinimizeForegroundWindow()
    {
        var window = GetTargetWindow();
        if (window != IntPtr.Zero)
        {
            _ = NativeMethods.ShowWindow(window, NativeMethods.SW_MINIMIZE);
        }
    }

    private static void CloseForegroundWindow()
    {
        var window = GetTargetWindow();
        if (window != IntPtr.Zero)
        {
            // Ask politely via the system menu so apps can prompt to save, veto, etc.
            _ = NativeMethods.PostMessageW(
                window, NativeMethods.WM_SYSCOMMAND, new IntPtr(NativeMethods.SC_CLOSE), IntPtr.Zero);
        }
    }

    public static void SendCtrlW()
    {
        var inputs = new[]
        {
            KeyInput(NativeMethods.VK_CONTROL, keyUp: false),
            KeyInput(NativeMethods.VK_W, keyUp: false),
            KeyInput(NativeMethods.VK_W, keyUp: true),
            KeyInput(NativeMethods.VK_CONTROL, keyUp: true),
        };

        _ = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static void SendMediaPlayPause()
    {
        var inputs = new[]
        {
            KeyInput(NativeMethods.VK_MEDIA_PLAY_PAUSE, keyUp: false),
            KeyInput(NativeMethods.VK_MEDIA_PLAY_PAUSE, keyUp: true),
        };

        _ = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static IntPtr GetTargetWindow()
    {
        var foregroundWindow = NativeMethods.GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var isShellWindow = foregroundWindow == NativeMethods.GetShellWindow();
        var className = NativeMethods.GetClassName(foregroundWindow);
        NativeMethods.GetWindowThreadProcessId(foregroundWindow, out var processId);

        return WindowFilter.ShouldTarget(className, isShellWindow, processId == Environment.ProcessId)
            ? foregroundWindow
            : IntPtr.Zero;
    }

    private static INPUT KeyInput(ushort virtualKey, bool keyUp) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = virtualKey,
                wScan = 0,
                dwFlags = keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0u,
                time = 0,
                dwExtraInfo = UIntPtr.Zero,
            }
        }
    };
}
