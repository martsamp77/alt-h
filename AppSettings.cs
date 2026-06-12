using System.Security;
using Microsoft.Win32;

namespace AltHMinimize;

/// <summary>
/// Legacy pre-0.4 single side-button setting; kept only to migrate old registry values
/// to the per-button <see cref="ButtonAction"/> settings.
/// </summary>
internal enum SideButton
{
    Off = 0,
    Back = 1,    // XBUTTON1
    Forward = 2, // XBUTTON2
}

/// <summary>
/// Persists the mouse-feature preferences under <c>HKCU\Software\AltHMinimize</c>.
/// Writes are best-effort: a failure leaves the in-memory toggle applied for the session.
/// Startup registration stays in the Run key and is handled separately.
/// </summary>
internal static class AppSettings
{
    private const string SettingsKey = @"Software\AltHMinimize";
    private const string MiddleClickValue = "MiddleClickCtrlW";
    private const string SideButtonValue = "SideButtonAction";
    private const string BackActionValue = "BackButtonAction";
    private const string ForwardActionValue = "ForwardButtonAction";
    private const string FirstRunShownValue = "FirstRunShown";

    public static bool LoadMiddleClickEnabled() => ReadDword(MiddleClickValue, defaultValue: 1) != 0;

    public static void SaveMiddleClickEnabled(bool enabled) => WriteDword(MiddleClickValue, enabled ? 1 : 0);

    /// <summary>Maps a raw registry value to a <see cref="SideButton"/>, defaulting to Forward.</summary>
    public static SideButton ParseSideButton(object? registryValue) =>
        registryValue is int value
            && value is >= (int)SideButton.Off and <= (int)SideButton.Forward
            ? (SideButton)value
            : SideButton.Forward;

    /// <summary>Maps a raw registry value to a <see cref="ButtonAction"/>.</summary>
    public static ButtonAction ParseButtonAction(object? registryValue, ButtonAction fallback) =>
        registryValue is int value
            && value is >= (int)ButtonAction.Off and <= (int)ButtonAction.MediaPlayPause
            ? (ButtonAction)value
            : fallback;

    /// <summary>
    /// Maps the legacy single side-button setting onto per-button actions. The configured
    /// button keeps its minimize behavior; the other starts Off. A missing legacy value
    /// parses as Forward, which matches the old fresh-install default.
    /// </summary>
    public static (ButtonAction Back, ButtonAction Forward) MigrateLegacySideButton(SideButton legacy) =>
        legacy switch
        {
            SideButton.Back => (ButtonAction.Minimize, ButtonAction.Off),
            SideButton.Forward => (ButtonAction.Off, ButtonAction.Minimize),
            _ => (ButtonAction.Off, ButtonAction.Off),
        };

    public static (ButtonAction Back, ButtonAction Forward) LoadButtonActions()
    {
        var back = ReadValue(BackActionValue);
        var forward = ReadValue(ForwardActionValue);

        if (back is null && forward is null)
        {
            var migrated = MigrateLegacySideButton(ParseSideButton(ReadValue(SideButtonValue)));
            SaveButtonActions(migrated.Back, migrated.Forward);
            DeleteValue(SideButtonValue);
            return migrated;
        }

        return (ParseButtonAction(back, ButtonAction.Off), ParseButtonAction(forward, ButtonAction.Off));
    }

    public static void SaveButtonActions(ButtonAction back, ButtonAction forward)
    {
        WriteDword(BackActionValue, (int)back);
        WriteDword(ForwardActionValue, (int)forward);
    }

    public static bool LoadFirstRunShown() => ReadDword(FirstRunShownValue, defaultValue: 0) != 0;

    public static void SaveFirstRunShown() => WriteDword(FirstRunShownValue, 1);

    private static int ReadDword(string name, int defaultValue) =>
        ReadValue(name) is int value ? value : defaultValue;

    private static object? ReadValue(string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(SettingsKey, writable: false);
            return key?.GetValue(name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return null;
        }
    }

    private static void DeleteValue(string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(SettingsKey, writable: true);
            key?.DeleteValue(name, throwOnMissingValue: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            // Best-effort cleanup of the legacy value.
        }
    }

    private static void WriteDword(string name, int value)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(SettingsKey, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(SettingsKey, writable: true);
            key.SetValue(name, value, RegistryValueKind.DWord);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            // Best-effort persistence; the in-memory toggle still applies for this session.
        }
    }
}
