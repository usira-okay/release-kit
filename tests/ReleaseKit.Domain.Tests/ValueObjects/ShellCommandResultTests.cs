using Xunit;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Tests.ValueObjects;

/// <summary>
/// ShellCommandResult 值物件測試
/// </summary>
public class ShellCommandResultTests
{
    [Fact]
    public void 成功結果_應正確設定屬性()
    {
        // Arrange & Act
        var result = new ShellCommandResult
        {
            StandardOutput = "output text",
            StandardError = "",
            ExitCode = 0,
            TimedOut = false
        };

        // Assert
        Assert.Equal("output text", result.StandardOutput);
        Assert.Empty(result.StandardError);
        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public void 超時結果_TimedOut應為True()
    {
        // Arrange & Act
        var result = new ShellCommandResult
        {
            StandardOutput = "",
            StandardError = "command timed out",
            ExitCode = -1,
            TimedOut = true
        };

        // Assert
        Assert.True(result.TimedOut);
        Assert.Equal(-1, result.ExitCode);
    }
}
