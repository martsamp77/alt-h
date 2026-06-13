using System.Diagnostics;

namespace AltHMinimize;

/// <summary>
/// Watches foreground-window changes via <c>SetWinEventHook</c> and reports the foreground
/// app's process name. With <c>WINEVENT_OUTOFCONTEXT</c> the callback is delivered on this
/// thread's message pump, so consumers run on the UI thread. Shell surfaces and this app's
/// own windows are filtered out through <see cref="WindowFilter"/>, so the last reported
/// name is always the app the user was actually working in.
/// </summary>
internal sealed class ForegroundWatcher : IDisposable
{
    private readonly Action<string> _onForegroundProcess;

    // Held in a field so the GC cannot collect the delegate while the native hook references it.
    private readonly NativeMethods.WinEventProc _callback;

    private IntPtr _hookHandle;
    private bool _disposed;

    public ForegroundWatcher(Action<string> onForegroundProcess)
    {
        _onForegroundProcess = onForegroundProcess;
        _callback = OnWinEvent;
    }

    public bool Start()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return true;
        }

        _hookHandle = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _callback,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT);

        if (_hookHandle == IntPtr.Zero)
        {
            return false;
        }

        // Evaluate the window that is already focused so state is correct before the
        // first foreground change.
        ReportForeground(NativeMethods.GetForegroundWindow());
        return true;
    }

    public void Stop()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.UnhookWinEvent(_hookHandle);
        _hookHandle = IntPtr.Zero;
    }

    private void OnWinEvent(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        => ReportForeground(hwnd);

    private void ReportForeground(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var isShellWindow = hwnd == NativeMethods.GetShellWindow();
        var className = NativeMethods.GetClassName(hwnd);
        NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);

        if (processId == 0 ||
            !WindowFilter.ShouldTarget(className, isShellWindow, processId == Environment.ProcessId))
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            _onForegroundProcess(process.ProcessName);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // The window's process exited between the event and the lookup.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
