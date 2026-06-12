namespace AltHMinimize;

/// <summary>
/// Pure decision for which foreground windows the minimize action may touch.
/// Shell surfaces (desktop, taskbar) and this app's own windows are excluded.
/// </summary>
internal static class WindowFilter
{
    public static bool ShouldMinimize(string? className, bool isShellWindow, bool isOwnProcess)
    {
        if (isShellWindow)
        {
            return false;
        }

        if (className is "Shell_TrayWnd" or "Progman" or "WorkerW")
        {
            return false;
        }

        return !isOwnProcess;
    }
}
