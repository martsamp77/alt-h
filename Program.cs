using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;

namespace AltHMinimize;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}

internal sealed class TrayApplicationContext : ApplicationContext
{
    private const string AppDisplayName = "Alt-H Minimize";
    private const string AppIconResourceName = "AltHMinimize.Assets.AltHMinimize.ico";
    private const string StartupValueName = "AltHMinimize";
    private const string StartupRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly HotKeyWindow _hotKeyWindow;
    private readonly MouseHook _mouseHook;
    private readonly Icon _trayIcon;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _enabledItem;
    private readonly ToolStripMenuItem _middleClickItem;
    private readonly ToolStripMenuItem _sideOffItem;
    private readonly ToolStripMenuItem _sideBackItem;
    private readonly ToolStripMenuItem _sideForwardItem;
    private readonly ToolStripMenuItem _startupItem;

    private bool _hotKeyEnabled = true;
    private bool _middleClickEnabled;
    private SideButton _sideButton;
    private bool _disposed;

    public TrayApplicationContext()
    {
        _hotKeyWindow = new HotKeyWindow(MinimizeForegroundWindow);

        _middleClickEnabled = AppSettings.LoadMiddleClickEnabled();
        _sideButton = AppSettings.LoadSideButton();
        _mouseHook = new MouseHook(SendCtrlW, MinimizeForegroundWindow)
        {
            MiddleClickEnabled = _middleClickEnabled,
            SideButtonAction = _sideButton,
        };

        _trayIcon = LoadAppIcon();

        _enabledItem = new ToolStripMenuItem("Alt+H Enabled")
        {
            Checked = true,
            CheckOnClick = false
        };
        _enabledItem.Click += (_, _) => SetHotKeyEnabled(!_hotKeyEnabled, showFailure: true);

        _middleClickItem = new ToolStripMenuItem("Middle-Click Closes Tab (Ctrl+W)")
        {
            Checked = _middleClickEnabled,
            CheckOnClick = false
        };
        _middleClickItem.Click += (_, _) => SetMiddleClickEnabled(!_middleClickEnabled);

        _sideOffItem = new ToolStripMenuItem("Off") { CheckOnClick = false };
        _sideOffItem.Click += (_, _) => SetSideButton(SideButton.Off);
        _sideBackItem = new ToolStripMenuItem("Back button (XBUTTON1)") { CheckOnClick = false };
        _sideBackItem.Click += (_, _) => SetSideButton(SideButton.Back);
        _sideForwardItem = new ToolStripMenuItem("Forward button (XBUTTON2)") { CheckOnClick = false };
        _sideForwardItem.Click += (_, _) => SetSideButton(SideButton.Forward);

        var sideButtonMenu = new ToolStripMenuItem("Side Button Minimizes");
        sideButtonMenu.DropDownItems.Add(_sideOffItem);
        sideButtonMenu.DropDownItems.Add(_sideBackItem);
        sideButtonMenu.DropDownItems.Add(_sideForwardItem);
        UpdateSideButtonChecks();

        _startupItem = new ToolStripMenuItem("Start with Windows")
        {
            Checked = IsStartupEnabled(),
            CheckOnClick = false
        };
        _startupItem.Click += (_, _) => ToggleStartup();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitThread();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_enabledItem);
        menu.Items.Add(_middleClickItem);
        menu.Items.Add(sideButtonMenu);
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = _trayIcon,
            Text = AppDisplayName,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ShowStatusBalloon();

        if (!SetHotKeyEnabled(enabled: true, showFailure: false))
        {
            ShowHotKeyFailure();
        }

        EnsureMouseHookState();
    }

    private bool SetHotKeyEnabled(bool enabled, bool showFailure)
    {
        if (enabled)
        {
            if (!_hotKeyWindow.RegisterAltH())
            {
                _hotKeyEnabled = false;
                _enabledItem.Checked = false;

                if (showFailure)
                {
                    ShowHotKeyFailure();
                }

                return false;
            }

            _hotKeyEnabled = true;
            _enabledItem.Checked = true;
            _notifyIcon.Text = AppDisplayName;
            return true;
        }

        _hotKeyWindow.UnregisterAltH();
        _hotKeyEnabled = false;
        _enabledItem.Checked = false;
        _notifyIcon.Text = $"{AppDisplayName} (disabled)";
        return true;
    }

    private void SetMiddleClickEnabled(bool enabled)
    {
        _middleClickEnabled = enabled;
        _mouseHook.MiddleClickEnabled = enabled;
        _middleClickItem.Checked = enabled;
        AppSettings.SaveMiddleClickEnabled(enabled);
        EnsureMouseHookState();
    }

    private void SetSideButton(SideButton button)
    {
        _sideButton = button;
        _mouseHook.SideButtonAction = button;
        UpdateSideButtonChecks();
        AppSettings.SaveSideButton(button);
        EnsureMouseHookState();
    }

    private void UpdateSideButtonChecks()
    {
        _sideOffItem.Checked = _sideButton == SideButton.Off;
        _sideBackItem.Checked = _sideButton == SideButton.Back;
        _sideForwardItem.Checked = _sideButton == SideButton.Forward;
    }

    private void EnsureMouseHookState()
    {
        var needHook = _middleClickEnabled || _sideButton != SideButton.Off;
        if (needHook)
        {
            if (!_mouseHook.IsInstalled && !_mouseHook.Install())
            {
                ShowMouseHookFailure();
            }

            return;
        }

        _mouseHook.Uninstall();
    }

    private void ToggleStartup()
    {
        try
        {
            if (IsStartupEnabled())
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupRunKey, writable: true);
                key?.DeleteValue(StartupValueName, throwOnMissingValue: false);
                _startupItem.Checked = false;
                return;
            }

            using var writeKey = Registry.CurrentUser.OpenSubKey(StartupRunKey, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(StartupRunKey, writable: true);
            writeKey.SetValue(StartupValueName, QuotePath(Application.ExecutablePath), RegistryValueKind.String);
            _startupItem.Checked = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            _notifyIcon.ShowBalloonTip(
                4000,
                AppDisplayName,
                $"Could not update Windows startup: {ex.Message}",
                ToolTipIcon.Error);
            _startupItem.Checked = IsStartupEnabled();
        }
    }

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupRunKey, writable: false);
        return string.Equals(
            key?.GetValue(StartupValueName) as string,
            QuotePath(Application.ExecutablePath),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string QuotePath(string path) => $"\"{path}\"";

    private static Icon LoadAppIcon()
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream(AppIconResourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource: {AppIconResourceName}");
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }

    private void ShowStatusBalloon()
    {
        var status = _hotKeyEnabled ? "Alt+H is enabled." : "Alt+H is disabled.";
        _notifyIcon.ShowBalloonTip(2500, AppDisplayName, status, ToolTipIcon.Info);
    }

    private void ShowHotKeyFailure()
    {
        _notifyIcon.ShowBalloonTip(
            5000,
            AppDisplayName,
            "Alt+H could not be registered. Another app may already be using it.",
            ToolTipIcon.Error);
    }

    private void ShowMouseHookFailure()
    {
        _notifyIcon.ShowBalloonTip(
            5000,
            AppDisplayName,
            "Mouse button actions could not be enabled. The low-level mouse hook failed to install.",
            ToolTipIcon.Error);
    }

    private static void MinimizeForegroundWindow()
    {
        var foregroundWindow = NativeMethods.GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
        {
            return;
        }

        var shellWindow = NativeMethods.GetShellWindow();
        if (foregroundWindow == shellWindow)
        {
            return;
        }

        var className = NativeMethods.GetClassName(foregroundWindow);
        if (className is "Shell_TrayWnd" or "Progman" or "WorkerW")
        {
            return;
        }

        NativeMethods.GetWindowThreadProcessId(foregroundWindow, out var processId);
        if (processId == Environment.ProcessId)
        {
            return;
        }

        _ = NativeMethods.ShowWindow(foregroundWindow, NativeMethods.SW_MINIMIZE);
    }

    private static void SendCtrlW()
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

    protected override void ExitThreadCore()
    {
        Dispose();
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _hotKeyWindow.Dispose();
            _mouseHook.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _trayIcon.Dispose();
        }

        _disposed = true;
        base.Dispose(disposing);
    }
}

internal sealed class HotKeyWindow : NativeWindow, IDisposable
{
    private const int HotKeyId = 1;
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint VK_H = 0x48;

    private readonly Action _onHotKey;
    private bool _registered;
    private bool _disposed;

    public HotKeyWindow(Action onHotKey)
    {
        _onHotKey = onHotKey;
        CreateHandle(new CreateParams());
    }

    public bool RegisterAltH()
    {
        if (_registered)
        {
            return true;
        }

        _registered = NativeMethods.RegisterHotKey(Handle, HotKeyId, MOD_ALT, VK_H);
        return _registered;
    }

    public void UnregisterAltH()
    {
        if (!_registered)
        {
            return;
        }

        NativeMethods.UnregisterHotKey(Handle, HotKeyId);
        _registered = false;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HotKeyId)
        {
            _onHotKey();
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

        UnregisterAltH();
        DestroyHandle();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
