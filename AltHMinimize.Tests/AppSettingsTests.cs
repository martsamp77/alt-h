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
}
