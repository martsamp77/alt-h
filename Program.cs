using System.ComponentModel;
using System.Diagnostics;
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
    private readonly Icon _trayIcon;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _enabledItem;
    private readonly ToolStripMenuItem _startupItem;

    private bool _hotKeyEnabled = true;
    private bool _disposed;

    public TrayApplicationContext()
    {
        _hotKeyWindow = new HotKeyWindow(MinimizeForegroundWindow);
        _trayIcon = LoadAppIcon();

        _enabledItem = new ToolStripMenuItem("Alt+H Enabled")
        {
            Checked = true,
            CheckOnClick = false
        };
        _enabledItem.Click += (_, _) => SetHotKeyEnabled(!_hotKeyEnabled, showFailure: true);

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

internal static partial class NativeMethods
{
    public const int SW_MINIMIZE = 6;

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(IntPtr hWnd, int id);

    [LibraryImport("user32.dll")]
    public static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    public static partial IntPtr GetShellWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial int GetClassNameCore(IntPtr hWnd, Span<char> lpClassName, int nMaxCount);

    public static string GetClassName(IntPtr hWnd)
    {
        Span<char> buffer = stackalloc char[256];
        var length = GetClassNameCore(hWnd, buffer, buffer.Length);
        return length <= 0 ? string.Empty : new string(buffer[..length]);
    }
}
