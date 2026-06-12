using Xunit;

namespace AltHMinimize.Tests;

public class WindowFilterTests
{
    [Fact]
    public void NormalWindow_ShouldMinimize()
    {
        Assert.True(WindowFilter.ShouldMinimize("Notepad", isShellWindow: false, isOwnProcess: false));
    }

    [Fact]
    public void NullClassName_ShouldMinimize()
    {
        Assert.True(WindowFilter.ShouldMinimize(null, isShellWindow: false, isOwnProcess: false));
    }

    [Fact]
    public void ShellWindow_ShouldNotMinimize()
    {
        Assert.False(WindowFilter.ShouldMinimize("Notepad", isShellWindow: true, isOwnProcess: false));
    }

    [Theory]
    [InlineData("Shell_TrayWnd")]
    [InlineData("Progman")]
    [InlineData("WorkerW")]
    public void ShellClassNames_ShouldNotMinimize(string className)
    {
        Assert.False(WindowFilter.ShouldMinimize(className, isShellWindow: false, isOwnProcess: false));
    }

    [Fact]
    public void OwnProcess_ShouldNotMinimize()
    {
        Assert.False(WindowFilter.ShouldMinimize("Notepad", isShellWindow: false, isOwnProcess: true));
    }
}
