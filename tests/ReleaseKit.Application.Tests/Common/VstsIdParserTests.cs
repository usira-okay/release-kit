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
    [InlineData("feature/vsts123-lowercase", 123)]      // 小寫也符合
    [InlineData("feature/Vsts456-mixed", 456)]          // 混合大小寫也符合
    [InlineData("feature/VsTs789-mixed", 789)]          // 混合大小寫也符合
    public void ParseFromSourceBranch_WithValidVSTSId_ShouldReturnId(string sourceBranch, int expectedId)
    {
        // Act
        var result = VstsIdParser.ParseFromSourceBranch(sourceBranch);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedId, result.Value);
    }

    [Theory]
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

    [Theory]
    [InlineData("VSTS12345 新增登入功能", 12345)]
    [InlineData("[VSTS99999] 修復問題", 99999)]
    [InlineData("vsts123: 更新文件", 123)]          // 小寫也符合
    [InlineData("Vsts456 - 重構程式碼", 456)]       // 混合大小寫也符合
    [InlineData("Feature: VSTS777 add auth", 777)]
    [InlineData("VSTS1", 1)]
    public void ParseFromTitle_WithValidVSTSId_ShouldReturnId(string title, int expectedId)
    {
        // Act
        var result = VstsIdParser.ParseFromTitle(title);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedId, result.Value);
    }

    [Theory]
    [InlineData("新增功能")]                        // 無 VSTS ID
    [InlineData("VSTSabc")]                       // 非數字
    [InlineData("VSTS")]                          // 無數字
    [InlineData("")]                              // 空字串
    [InlineData(null)]                            // null
    [InlineData("   ")]                           // 空白字串
    public void ParseFromTitle_WithInvalidFormat_ShouldReturnNull(string? title)
    {
        // Act
        var result = VstsIdParser.ParseFromTitle(title);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("feature/VSTS12345-add-login", "修改功能", 12345)]  // SourceBranch 有 ID
    [InlineData("feature/VSTS111-branch", "VSTS222 title", 111)]   // 兩者都有，優先 SourceBranch
    [InlineData(null, "VSTS12345 標題", 12345)]                    // SourceBranch 為 null，使用 Title
    [InlineData("", "VSTS67890 標題", 67890)]                      // SourceBranch 為空，使用 Title
    [InlineData("   ", "VSTS54321 標題", 54321)]                   // SourceBranch 為空白，使用 Title
    public void Parse_WithValidInput_ShouldReturnId(string? sourceBranch, string? title, int expectedId)
    {
        // Act
        var result = VstsIdParser.Parse(sourceBranch, title);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedId, result.Value);
    }

    [Theory]
    [InlineData("feature/no-id", "VSTS99999 新增功能")]  // SourceBranch 有值但無 ID，不 fallback 到 Title
    [InlineData("feature/no-id", "無ID的標題")]          // 兩者都沒有 ID
    [InlineData(null, null)]                            // 兩者都是 null
    [InlineData("", "")]                                // 兩者都是空字串
    [InlineData("   ", "   ")]                          // 兩者都是空白
    public void Parse_WithoutVSTSId_ShouldReturnNull(string? sourceBranch, string? title)
    {
        // Act
        var result = VstsIdParser.Parse(sourceBranch, title);

        // Assert
        Assert.Null(result);
    }
}
