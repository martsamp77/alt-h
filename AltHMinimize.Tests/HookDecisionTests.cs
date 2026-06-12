using Xunit;

namespace AltHMinimize.Tests;

public class HookDecisionTests
{
    private static uint XButtonData(ushort xButton) => (uint)xButton << 16;

    [Theory]
    [InlineData(NativeMethods.WM_MBUTTONDOWN, (int)HookAction.SuppressAndPostMiddle)]
    [InlineData(NativeMethods.WM_MBUTTONUP, (int)HookAction.Suppress)]
    public void MiddleClick_Enabled_SuppressesBothAndPostsOnDown(int message, int expected)
    {
        var action = HookDecision.Decide(message, 0, 0, middleClickEnabled: true, SideButton.Off);
        Assert.Equal((HookAction)expected, action);
    }

    [Theory]
    [InlineData(NativeMethods.WM_MBUTTONDOWN)]
    [InlineData(NativeMethods.WM_MBUTTONUP)]
    public void MiddleClick_Disabled_PassesThrough(int message)
    {
        var action = HookDecision.Decide(message, 0, 0, middleClickEnabled: false, SideButton.Forward);
        Assert.Equal(HookAction.Pass, action);
    }

    [Theory]
    [InlineData((int)SideButton.Back, NativeMethods.XBUTTON1)]
    [InlineData((int)SideButton.Forward, NativeMethods.XBUTTON2)]
    public void SideButton_ConfiguredButton_SuppressesAndPostsOnDown(int configured, ushort xButton)
    {
        var sideButton = (SideButton)configured;
        var down = HookDecision.Decide(
            NativeMethods.WM_XBUTTONDOWN, XButtonData(xButton), 0, middleClickEnabled: false, sideButton);
        var up = HookDecision.Decide(
            NativeMethods.WM_XBUTTONUP, XButtonData(xButton), 0, middleClickEnabled: false, sideButton);

        Assert.Equal(HookAction.SuppressAndPostSide, down);
        Assert.Equal(HookAction.Suppress, up);
    }

    [Theory]
    [InlineData((int)SideButton.Back, NativeMethods.XBUTTON2)]
    [InlineData((int)SideButton.Forward, NativeMethods.XBUTTON1)]
    public void SideButton_OtherButton_PassesThrough(int configured, ushort xButton)
    {
        var action = HookDecision.Decide(
            NativeMethods.WM_XBUTTONDOWN, XButtonData(xButton), 0, middleClickEnabled: false, (SideButton)configured);
        Assert.Equal(HookAction.Pass, action);
    }

    [Theory]
    [InlineData(NativeMethods.XBUTTON1)]
    [InlineData(NativeMethods.XBUTTON2)]
    public void SideButton_Off_PassesThrough(ushort xButton)
    {
        var action = HookDecision.Decide(
            NativeMethods.WM_XBUTTONDOWN, XButtonData(xButton), 0, middleClickEnabled: false, SideButton.Off);
        Assert.Equal(HookAction.Pass, action);
    }

    [Fact]
    public void SideButton_InjectedEvent_PassesThrough()
    {
        var action = HookDecision.Decide(
            NativeMethods.WM_XBUTTONDOWN,
            XButtonData(NativeMethods.XBUTTON2),
            NativeMethods.LLMHF_INJECTED,
            middleClickEnabled: false,
            SideButton.Forward);
        Assert.Equal(HookAction.Pass, action);
    }

    [Fact]
    public void UnrelatedMessage_PassesThrough()
    {
        const int WM_LBUTTONDOWN = 0x0201;
        var action = HookDecision.Decide(
            WM_LBUTTONDOWN, 0, 0, middleClickEnabled: true, SideButton.Forward);
        Assert.Equal(HookAction.Pass, action);
    }
}
