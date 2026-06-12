using Xunit;

namespace AltHMinimize.Tests;

public class HookDecisionTests
{
    private static uint XButtonData(ushort xButton) => (uint)xButton << 16;

    private static HookAction Decide(
        int message,
        uint mouseData = 0,
        uint flags = 0,
        bool middleClick = false,
        bool back = false,
        bool forward = false)
        => HookDecision.Decide(message, mouseData, flags, middleClick, back, forward);

    [Theory]
    [InlineData(NativeMethods.WM_MBUTTONDOWN, (int)HookAction.SuppressAndPostMiddle)]
    [InlineData(NativeMethods.WM_MBUTTONUP, (int)HookAction.Suppress)]
    public void MiddleClick_Enabled_SuppressesBothAndPostsOnDown(int message, int expected)
    {
        Assert.Equal((HookAction)expected, Decide(message, middleClick: true));
    }

    [Theory]
    [InlineData(NativeMethods.WM_MBUTTONDOWN)]
    [InlineData(NativeMethods.WM_MBUTTONUP)]
    public void MiddleClick_Disabled_PassesThrough(int message)
    {
        Assert.Equal(HookAction.Pass, Decide(message, middleClick: false, back: true, forward: true));
    }

    [Theory]
    [InlineData(NativeMethods.XBUTTON1, true, false)]
    [InlineData(NativeMethods.XBUTTON2, false, true)]
    public void SideButton_EnabledButton_SuppressesAndPostsOnDown(ushort xButton, bool back, bool forward)
    {
        var down = Decide(NativeMethods.WM_XBUTTONDOWN, XButtonData(xButton), back: back, forward: forward);
        var up = Decide(NativeMethods.WM_XBUTTONUP, XButtonData(xButton), back: back, forward: forward);

        Assert.Equal(HookAction.SuppressAndPostSide, down);
        Assert.Equal(HookAction.Suppress, up);
    }

    [Theory]
    [InlineData(NativeMethods.XBUTTON1, false, true)]
    [InlineData(NativeMethods.XBUTTON2, true, false)]
    public void SideButton_OtherButtonEnabled_PassesThrough(ushort xButton, bool back, bool forward)
    {
        var action = Decide(NativeMethods.WM_XBUTTONDOWN, XButtonData(xButton), back: back, forward: forward);
        Assert.Equal(HookAction.Pass, action);
    }

    [Fact]
    public void SideButton_BothEnabled_SuppressesEither()
    {
        var backDown = Decide(
            NativeMethods.WM_XBUTTONDOWN, XButtonData(NativeMethods.XBUTTON1), back: true, forward: true);
        var forwardDown = Decide(
            NativeMethods.WM_XBUTTONDOWN, XButtonData(NativeMethods.XBUTTON2), back: true, forward: true);

        Assert.Equal(HookAction.SuppressAndPostSide, backDown);
        Assert.Equal(HookAction.SuppressAndPostSide, forwardDown);
    }

    [Theory]
    [InlineData(NativeMethods.XBUTTON1)]
    [InlineData(NativeMethods.XBUTTON2)]
    public void SideButton_NoneEnabled_PassesThrough(ushort xButton)
    {
        var action = Decide(NativeMethods.WM_XBUTTONDOWN, XButtonData(xButton));
        Assert.Equal(HookAction.Pass, action);
    }

    [Fact]
    public void SideButton_InjectedEvent_PassesThrough()
    {
        var action = Decide(
            NativeMethods.WM_XBUTTONDOWN,
            XButtonData(NativeMethods.XBUTTON2),
            NativeMethods.LLMHF_INJECTED,
            back: true,
            forward: true);
        Assert.Equal(HookAction.Pass, action);
    }

    [Fact]
    public void UnrelatedMessage_PassesThrough()
    {
        const int WM_LBUTTONDOWN = 0x0201;
        Assert.Equal(HookAction.Pass, Decide(WM_LBUTTONDOWN, middleClick: true, back: true, forward: true));
    }
}
