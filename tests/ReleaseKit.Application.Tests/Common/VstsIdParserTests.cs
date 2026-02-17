using ReleaseKit.Application.Common;
using Xunit;

namespace ReleaseKit.Application.Tests.Common;

/// <summary>
/// VstsIdParser 單元測試
/// </summary>
public class VstsIdParserTests
{
    [Theory]
    [InlineData("feature/VSTS12345-add-login", 12345)]
    [InlineData("VSTS99999", 99999)]
    [InlineData("bugfix/VSTS777-fix-bug", 777)]
    [InlineData("release/VSTS1-init", 1)]
    [InlineData("hotfix/VSTS123456789", 123456789)]
    public void ParseFromSourceBranch_WithValidVSTSId_ShouldReturnId(string sourceBranch, int expectedId)
    {
        // Act
        var result = VstsIdParser.ParseFromSourceBranch(sourceBranch);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedId, result.Value);
    }

    [Theory]
    [InlineData("feature/vsts123-lowercase")]     // 小寫不符合
    [InlineData("feature/no-id")]                 // 無 VSTS ID
    [InlineData("VSTSabc")]                       // 非數字
    [InlineData("VSTS")]                          // 無數字
    [InlineData("feature/VSTS-123")]              // 格式錯誤（有連字號）
    [InlineData("")]                              // 空字串
    [InlineData(null)]                            // null
    [InlineData("   ")]                           // 空白字串
    public void ParseFromSourceBranch_WithInvalidFormat_ShouldReturnNull(string? sourceBranch)
    {
        // Act
        var result = VstsIdParser.ParseFromSourceBranch(sourceBranch);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ParseFromSourceBranch_WithMultipleVSTSIds_ShouldReturnFirstOne()
    {
        // Arrange
        var sourceBranch = "feature/VSTS111-and-VSTS222";

        // Act
        var result = VstsIdParser.ParseFromSourceBranch(sourceBranch);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(111, result.Value);
    }
}
