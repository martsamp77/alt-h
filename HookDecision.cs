namespace AltHMinimize;

internal enum HookAction
{
    Pass,
    Suppress,
    SuppressAndPostMiddle,
    SuppressAndPostSide,
}

/// <summary>
/// Pure decision core of the low-level mouse hook callback: given an input event and the
/// current configuration, decides whether to pass it through, suppress it, or suppress it
/// and dispatch an action. Kept free of interop so it can be unit tested.
/// </summary>
internal static class HookDecision
{
    public static HookAction Decide(
        int message,
        uint mouseData,
        uint flags,
        bool middleClickEnabled,
        SideButton sideButton)
    {
        if (middleClickEnabled &&
            (message == NativeMethods.WM_MBUTTONDOWN || message == NativeMethods.WM_MBUTTONUP))
        {
            // Suppress both down and up, but dispatch only on down.
            return message == NativeMethods.WM_MBUTTONDOWN
                ? HookAction.SuppressAndPostMiddle
                : HookAction.Suppress;
        }

        if (sideButton != SideButton.Off &&
            (message == NativeMethods.WM_XBUTTONDOWN || message == NativeMethods.WM_XBUTTONUP))
        {
            // Ignore synthetic events to avoid acting on input we (or other tools) injected.
            if ((flags & NativeMethods.LLMHF_INJECTED) != 0)
            {
                return HookAction.Pass;
            }

            var xButton = (ushort)((mouseData >> 16) & 0xFFFF);
            var match =
                (sideButton == SideButton.Back && xButton == NativeMethods.XBUTTON1) ||
                (sideButton == SideButton.Forward && xButton == NativeMethods.XBUTTON2);

            if (match)
            {
                // Suppress only the configured side button; dispatch only on down.
                return message == NativeMethods.WM_XBUTTONDOWN
                    ? HookAction.SuppressAndPostSide
                    : HookAction.Suppress;
            }
        }

        return HookAction.Pass;
    }
}
