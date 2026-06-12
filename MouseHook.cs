using System.Runtime.InteropServices;

namespace AltHMinimize;

/// <summary>
/// Installs a global low-level mouse hook (<c>WH_MOUSE_LL</c>) on the thread that owns it
/// (the WinForms UI thread, which runs the message pump). The hook callback only decides
/// whether to suppress an event and posts a window message; the configured action runs from
/// <see cref="WndProc"/> on the next pump cycle so the callback returns well within the
/// <c>LowLevelHooksTimeout</c> window.
/// </summary>
internal sealed class MouseHook : NativeWindow, IDisposable
{
    private const int WM_APP_MIDDLE = 0x8000 + 1; // WM_APP + 1
    private const int WM_APP_SIDE = 0x8000 + 2;   // WM_APP + 2

    private readonly Action _onMiddleClick;
    private readonly Action<ushort> _onSideButton;

    // Held in a field so the GC cannot collect the delegate while the native hook references it.
    private readonly NativeMethods.HookProc _hookProc;

    private IntPtr _hookHandle;
    private bool _disposed;

    // Read inside the hook callback, written from the foreground watcher; volatile keeps
    // the hot path to a single flag check with no locking.
    private volatile bool _suppressionPaused;

    public MouseHook(Action onMiddleClick, Action<ushort> onSideButton)
    {
        _onMiddleClick = onMiddleClick;
        _onSideButton = onSideButton;
        _hookProc = HookCallback;
        CreateHandle(new CreateParams());
    }

    public bool MiddleClickEnabled { get; set; }

    public bool BackButtonEnabled { get; set; }

    public bool ForwardButtonEnabled { get; set; }

    /// <summary>While true (foreground app is excluded), every event passes through untouched.</summary>
    public bool SuppressionPaused
    {
        get => _suppressionPaused;
        set => _suppressionPaused = value;
    }

    public bool IsInstalled => _hookHandle != IntPtr.Zero;

    public bool Install()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return true;
        }

        _hookHandle = NativeMethods.SetWindowsHookExW(
            NativeMethods.WH_MOUSE_LL,
            _hookProc,
            NativeMethods.GetModuleHandle(null),
            0);

        return _hookHandle != IntPtr.Zero;
    }

    public void Uninstall()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == NativeMethods.HC_ACTION && !_suppressionPaused)
        {
            var message = wParam.ToInt32();

            uint mouseData = 0;
            uint flags = 0;
            if (message == NativeMethods.WM_XBUTTONDOWN || message == NativeMethods.WM_XBUTTONUP)
            {
                var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                mouseData = data.mouseData;
                flags = data.flags;
            }

            switch (HookDecision.Decide(message, mouseData, flags, MiddleClickEnabled, BackButtonEnabled, ForwardButtonEnabled))
            {
                case HookAction.SuppressAndPostMiddle:
                    NativeMethods.PostMessageW(Handle, WM_APP_MIDDLE, IntPtr.Zero, IntPtr.Zero);
                    return 1;
                case HookAction.SuppressAndPostSide:
                    var xButton = (ushort)((mouseData >> 16) & 0xFFFF);
                    NativeMethods.PostMessageW(Handle, WM_APP_SIDE, new IntPtr(xButton), IntPtr.Zero);
                    return 1;
                case HookAction.Suppress:
                    return 1;
            }
        }

        return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_APP_MIDDLE:
                _onMiddleClick();
                return;
            case WM_APP_SIDE:
                _onSideButton((ushort)m.WParam);
                return;
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Uninstall();
        DestroyHandle();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
