using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;

namespace AltHMinimize;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // A second instance would register a second mouse hook (double Ctrl+W per
        // middle click) and fail hotkey registration; exit silently instead.
        using var instanceMutex = new Mutex(initiallyOwned: true, "AltHMinimize.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            return;
        }

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
    private readonly Dictionary<ButtonAction, ToolStripMenuItem> _backItems = [];
    private readonly Dictionary<ButtonAction, ToolStripMenuItem> _forwardItems = [];
    private readonly ToolStripMenuItem _startupItem;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _excludedMenu;
    private readonly ForegroundWatcher _foregroundWatcher;
    private readonly List<string> _excludedProcesses;

    private bool _hotKeyEnabled = true;
    private bool _middleClickEnabled;
    private ButtonAction _backAction;
    private ButtonAction _forwardAction;
    private string? _lastForegroundProcess;
    private bool _paused;
    private bool _disposed;

    public TrayApplicationContext()
    {
        _hotKeyWindow = new HotKeyWindow(Actions.MinimizeForegroundWindow);

        _middleClickEnabled = AppSettings.LoadMiddleClickEnabled();
        (_backAction, _forwardAction) = AppSettings.LoadButtonActions();
        _excludedProcesses = [.. AppSettings.LoadExcludedProcesses()];
        _mouseHook = new MouseHook(Actions.SendCtrlW, OnSideButton)
        {
            MiddleClickEnabled = _middleClickEnabled,
            BackButtonEnabled = _backAction != ButtonAction.Off,
            ForwardButtonEnabled = _forwardAction != ButtonAction.Off,
        };
        _foregroundWatcher = new ForegroundWatcher(OnForegroundProcessChanged);

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

        var backMenu = CreateButtonActionMenu(
            "Back Button (XBUTTON1)", _backItems, action => SetButtonAction(isBack: true, action));
        var forwardMenu = CreateButtonActionMenu(
            "Forward Button (XBUTTON2)", _forwardItems, action => SetButtonAction(isBack: false, action));
        UpdateButtonActionChecks();

        _excludedMenu = new ToolStripMenuItem("Excluded Apps");
        _excludedMenu.DropDownOpening += (_, _) => RebuildExcludedMenu();
        RebuildExcludedMenu();

        _startupItem = new ToolStripMenuItem("Start with Windows")
        {
            Checked = IsStartupEnabled(),
            CheckOnClick = false
        };
        _startupItem.Click += (_, _) => ToggleStartup();

        _pauseItem = new ToolStripMenuItem("Pause All")
        {
            Checked = false,
            CheckOnClick = false
        };
        _pauseItem.Click += (_, _) => SetPaused(!_paused);

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitThread();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_enabledItem);
        menu.Items.Add(_middleClickItem);
        menu.Items.Add(backMenu);
        menu.Items.Add(forwardMenu);
        menu.Items.Add(_excludedMenu);
        menu.Items.Add(_startupItem);
        menu.Items.Add(_pauseItem);
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
        _foregroundWatcher.Start();
        ShowFirstRunBalloon();
    }

    private void OnForegroundProcessChanged(string processName)
    {
        _lastForegroundProcess = processName;
        _mouseHook.SuppressionPaused = _excludedProcesses.Contains(processName, StringComparer.OrdinalIgnoreCase);
    }

    private void RebuildExcludedMenu()
    {
        _excludedMenu.DropDownItems.Clear();

        var current = _lastForegroundProcess;
        var canAdd = current is not null
            && !_excludedProcesses.Contains(current, StringComparer.OrdinalIgnoreCase);
        var addItem = new ToolStripMenuItem(current is null ? "Add Current App" : $"Add Current App ({current})")
        {
            Enabled = canAdd
        };
        addItem.Click += (_, _) => AddExcludedProcess();
        _excludedMenu.DropDownItems.Add(addItem);
        _excludedMenu.DropDownItems.Add(new ToolStripSeparator());

        if (_excludedProcesses.Count == 0)
        {
            _excludedMenu.DropDownItems.Add(new ToolStripMenuItem("(none — mouse actions apply everywhere)")
            {
                Enabled = false
            });
            return;
        }

        foreach (var name in _excludedProcesses)
        {
            var item = new ToolStripMenuItem($"{name} — click to remove");
            item.Click += (_, _) => RemoveExcludedProcess(name);
            _excludedMenu.DropDownItems.Add(item);
        }
    }

    private void AddExcludedProcess()
    {
        if (_lastForegroundProcess is not { } name
            || _excludedProcesses.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _excludedProcesses.Add(name);
        AppSettings.SaveExcludedProcesses(_excludedProcesses);
        OnExcludedProcessesChanged();
    }

    private void RemoveExcludedProcess(string name)
    {
        _excludedProcesses.RemoveAll(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase));
        AppSettings.SaveExcludedProcesses(_excludedProcesses);
        OnExcludedProcessesChanged();
    }

    private void OnExcludedProcessesChanged()
    {
        // Re-evaluate against the current foreground app so the change applies immediately.
        _mouseHook.SuppressionPaused = _lastForegroundProcess is { } current
            && _excludedProcesses.Contains(current, StringComparer.OrdinalIgnoreCase);
    }

    private void ShowFirstRunBalloon()
    {
        if (AppSettings.LoadFirstRunShown())
        {
            return;
        }

        _notifyIcon.ShowBalloonTip(
            8000,
            AppDisplayName,
            "Alt+H minimizes the focused window. Middle-click now sends Ctrl+W (close tab) "
                + "and the Forward side button minimizes — both can be changed in this tray menu.",
            ToolTipIcon.Info);
        AppSettings.SaveFirstRunShown();
    }

    private bool SetHotKeyEnabled(bool enabled, bool showFailure)
    {
        if (enabled)
        {
            // While paused, only record the intent; registration happens on resume.
            if (!_paused && !_hotKeyWindow.RegisterAltH())
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
            UpdateTrayText();
            return true;
        }

        _hotKeyWindow.UnregisterAltH();
        _hotKeyEnabled = false;
        _enabledItem.Checked = false;
        UpdateTrayText();
        return true;
    }

    private void SetPaused(bool paused)
    {
        _paused = paused;
        _pauseItem.Checked = paused;

        if (paused)
        {
            _hotKeyWindow.UnregisterAltH();
            _mouseHook.Uninstall();
        }
        else
        {
            if (_hotKeyEnabled)
            {
                SetHotKeyEnabled(enabled: true, showFailure: true);
            }

            EnsureMouseHookState();
        }

        UpdateTrayText();
    }

    private void UpdateTrayText()
    {
        _notifyIcon.Text = _paused
            ? $"{AppDisplayName} (paused)"
            : _hotKeyEnabled
                ? AppDisplayName
                : $"{AppDisplayName} (disabled)";
    }

    private void SetMiddleClickEnabled(bool enabled)
    {
        _middleClickEnabled = enabled;
        _mouseHook.MiddleClickEnabled = enabled;
        _middleClickItem.Checked = enabled;
        AppSettings.SaveMiddleClickEnabled(enabled);
        EnsureMouseHookState();
    }

    private static readonly (ButtonAction Action, string Label)[] _actionLabels =
    [
        (ButtonAction.Off, "Off"),
        (ButtonAction.Minimize, "Minimize Window"),
        (ButtonAction.CloseWindow, "Close Window"),
        (ButtonAction.CtrlW, "Close Tab (Ctrl+W)"),
        (ButtonAction.MediaPlayPause, "Play/Pause Media"),
    ];

    private static ToolStripMenuItem CreateButtonActionMenu(
        string title,
        Dictionary<ButtonAction, ToolStripMenuItem> items,
        Action<ButtonAction> onSelect)
    {
        var menu = new ToolStripMenuItem(title);
        foreach (var (action, label) in _actionLabels)
        {
            var item = new ToolStripMenuItem(label) { CheckOnClick = false };
            item.Click += (_, _) => onSelect(action);
            items[action] = item;
            menu.DropDownItems.Add(item);
        }

        return menu;
    }

    private void SetButtonAction(bool isBack, ButtonAction action)
    {
        if (isBack)
        {
            _backAction = action;
            _mouseHook.BackButtonEnabled = action != ButtonAction.Off;
        }
        else
        {
            _forwardAction = action;
            _mouseHook.ForwardButtonEnabled = action != ButtonAction.Off;
        }

        UpdateButtonActionChecks();
        AppSettings.SaveButtonActions(_backAction, _forwardAction);
        EnsureMouseHookState();
    }

    private void UpdateButtonActionChecks()
    {
        foreach (var (action, item) in _backItems)
        {
            item.Checked = action == _backAction;
        }

        foreach (var (action, item) in _forwardItems)
        {
            item.Checked = action == _forwardAction;
        }
    }

    private void OnSideButton(ushort xButton) =>
        Actions.Execute(xButton == NativeMethods.XBUTTON1 ? _backAction : _forwardAction);

    private void EnsureMouseHookState()
    {
        var needHook = !_paused &&
            (_middleClickEnabled || _backAction != ButtonAction.Off || _forwardAction != ButtonAction.Off);
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
        var status = _paused
            ? "Alt-H is paused."
            : _hotKeyEnabled
                ? "Alt+H is enabled."
                : "Alt+H is disabled.";
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
            _foregroundWatcher.Dispose();
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
