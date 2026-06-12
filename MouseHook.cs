using System.Runtime.InteropServices;

namespace AltHMinimize;

internal enum SideButton
{
    Off = 0,
    Back = 1,    // XBUTTON1
    Forward = 2, // XBUTTON2
}

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
    private readonly Action _onSideButton;

    // Held in a field so the GC cannot collect the delegate while the native hook references it.
    private readonly NativeMethods.HookProc _hookProc;

    private IntPtr _hookHandle;
    private bool _disposed;

    public MouseHook(Action onMiddleClick, Action onSideButton)
    {
        _onMiddleClick = onMiddleClick;
        _onSideButton = onSideButton;
        _hookProc = HookCallback;
        CreateHandle(new CreateParams());
    }

    public bool MiddleClickEnabled { get; set; }

    public SideButton SideButtonAction { get; set; }

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
        if (nCode == NativeMethods.HC_ACTION)
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

            switch (HookDecision.Decide(message, mouseData, flags, MiddleClickEnabled, SideButtonAction))
            {
                case HookAction.SuppressAndPostMiddle:
                    NativeMethods.PostMessageW(Handle, WM_APP_MIDDLE, IntPtr.Zero, IntPtr.Zero);
                    return 1;
                case HookAction.SuppressAndPostSide:
                    NativeMethods.PostMessageW(Handle, WM_APP_SIDE, IntPtr.Zero, IntPtr.Zero);
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
                _onSideButton();
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
