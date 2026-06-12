using Xunit;

namespace AltHMinimize.Tests;

public class AppSettingsTests
{
    [Theory]
    [InlineData(0, (int)SideButton.Off)]
    [InlineData(1, (int)SideButton.Back)]
    [InlineData(2, (int)SideButton.Forward)]
    public void ParseSideButton_ValidValue_Maps(int raw, int expected)
    {
        Assert.Equal((SideButton)expected, AppSettings.ParseSideButton(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData("Forward")]
    [InlineData(2L)]
    public void ParseSideButton_MissingOrInvalid_DefaultsToForward(object? raw)
    {
        Assert.Equal(SideButton.Forward, AppSettings.ParseSideButton(raw));
    }

    [Theory]
    [InlineData(0, (int)ButtonAction.Off)]
    [InlineData(1, (int)ButtonAction.Minimize)]
    [InlineData(2, (int)ButtonAction.CloseWindow)]
    [InlineData(3, (int)ButtonAction.CtrlW)]
    [InlineData(4, (int)ButtonAction.MediaPlayPause)]
    public void ParseButtonAction_ValidValue_Maps(int raw, int expected)
    {
        Assert.Equal((ButtonAction)expected, AppSettings.ParseButtonAction(raw, ButtonAction.Off));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData("Minimize")]
    public void ParseButtonAction_MissingOrInvalid_UsesFallback(object? raw)
    {
        Assert.Equal(ButtonAction.Minimize, AppSettings.ParseButtonAction(raw, ButtonAction.Minimize));
    }

    [Theory]
    [InlineData((int)SideButton.Off, (int)ButtonAction.Off, (int)ButtonAction.Off)]
    [InlineData((int)SideButton.Back, (int)ButtonAction.Minimize, (int)ButtonAction.Off)]
    [InlineData((int)SideButton.Forward, (int)ButtonAction.Off, (int)ButtonAction.Minimize)]
    public void MigrateLegacySideButton_KeepsConfiguredButtonMinimizing(int legacy, int expectedBack, int expectedForward)
    {
        var (back, forward) = AppSettings.MigrateLegacySideButton((SideButton)legacy);

        Assert.Equal((ButtonAction)expectedBack, back);
        Assert.Equal((ButtonAction)expectedForward, forward);
    }
}
