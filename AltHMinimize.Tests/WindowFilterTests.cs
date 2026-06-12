using Xunit;

namespace AltHMinimize.Tests;

public class WindowFilterTests
{
    [Fact]
    public void NormalWindow_IsTargeted()
    {
        Assert.True(WindowFilter.ShouldTarget("Notepad", isShellWindow: false, isOwnProcess: false));
    }

    [Fact]
    public void NullClassName_IsTargeted()
    {
        Assert.True(WindowFilter.ShouldTarget(null, isShellWindow: false, isOwnProcess: false));
    }

    [Fact]
    public void ShellWindow_IsNotTargeted()
    {
        Assert.False(WindowFilter.ShouldTarget("Notepad", isShellWindow: true, isOwnProcess: false));
    }

    [Theory]
    [InlineData("Shell_TrayWnd")]
    [InlineData("Progman")]
    [InlineData("WorkerW")]
    public void ShellClassNames_AreNotTargeted(string className)
    {
        Assert.False(WindowFilter.ShouldTarget(className, isShellWindow: false, isOwnProcess: false));
    }

    [Fact]
    public void OwnProcess_IsNotTargeted()
    {
        Assert.False(WindowFilter.ShouldTarget("Notepad", isShellWindow: false, isOwnProcess: true));
    }
}
