using System.Security;
using Microsoft.Win32;

namespace AltHMinimize;

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

    public static bool LoadMiddleClickEnabled() => ReadDword(MiddleClickValue, defaultValue: 1) != 0;

    public static void SaveMiddleClickEnabled(bool enabled) => WriteDword(MiddleClickValue, enabled ? 1 : 0);

    public static SideButton LoadSideButton() => ParseSideButton(ReadValue(SideButtonValue));

    /// <summary>Maps a raw registry value to a <see cref="SideButton"/>, defaulting to Forward.</summary>
    public static SideButton ParseSideButton(object? registryValue) =>
        registryValue is int value
            && value is >= (int)SideButton.Off and <= (int)SideButton.Forward
            ? (SideButton)value
            : SideButton.Forward;

    public static void SaveSideButton(SideButton button) => WriteDword(SideButtonValue, (int)button);

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
