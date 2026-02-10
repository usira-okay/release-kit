using ReleaseKit.Domain.Helpers;
using Xunit;

namespace ReleaseKit.Domain.Tests.Helpers;

/// <summary>
/// ReleaseBranchHelper 單元測試
/// </summary>
public class ReleaseBranchHelperTests
{
    [Theory]
    [InlineData("release/20250101", true)]
    [InlineData("release/20241231", true)]
    [InlineData("release/20990101", true)]
    [InlineData("Release/20250101", true)]  // 大寫 R
    [InlineData("RELEASE/20250101", true)]  // 全大寫
    [InlineData("ReLease/20250101", true)]  // 混合大小寫
    [InlineData("release/2025010", false)]  // 7 位數字
    [InlineData("release/202501011", false)]  // 9 位數字
    [InlineData("release/abcd1234", false)]  // 非數字
    [InlineData("feature/20250101", false)]  // 非 release 前綴
    [InlineData("releases/20250101", false)]  // 複數形式
    [InlineData("release/20250101-fix", false)]  // 有額外後綴
    [InlineData("dev-release/20250101", false)]  // 有前綴
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsReleaseBranch_WithVariousInputs_ShouldReturnExpectedResult(string? branchName, bool expected)
    {
        // Act
        var result = ReleaseBranchHelper.IsReleaseBranch(branchName);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseReleaseBranchDate_WithValidBranch_ShouldReturnCorrectDate()
    {
        // Arrange
        var branchName = "release/20250315";
        var expectedDate = new DateTimeOffset(2025, 3, 15, 0, 0, 0, TimeSpan.Zero);

        // Act
        var result = ReleaseBranchHelper.ParseReleaseBranchDate(branchName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedDate.Year, result.Value.Year);
        Assert.Equal(expectedDate.Month, result.Value.Month);
        Assert.Equal(expectedDate.Day, result.Value.Day);
    }

    [Theory]
    [InlineData("release/20251301")]  // 無效月份
    [InlineData("release/20250132")]  // 無效日期
    [InlineData("release/abcd1234")]  // 非數字
    [InlineData("feature/20250101")]  // 非 release branch
    [InlineData("")]
    [InlineData(null)]
    public void ParseReleaseBranchDate_WithInvalidBranch_ShouldReturnNull(string? branchName)
    {
        // Act
        var result = ReleaseBranchHelper.ParseReleaseBranchDate(branchName);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void SortReleaseBranchesDescending_WithMultipleBranches_ShouldReturnSortedList()
    {
        // Arrange
        var branches = new List<string>
        {
            "release/20250101",
            "release/20241201",
            "release/20250315",
            "release/20240601",
            "feature/test",  // 應被過濾掉
            "main",  // 應被過濾掉
            "release/20241215"
        };

        // Act
        var result = ReleaseBranchHelper.SortReleaseBranchesDescending(branches);

        // Assert
        Assert.Equal(5, result.Count);
        Assert.Equal("release/20250315", result[0]);  // 最新
        Assert.Equal("release/20250101", result[1]);
        Assert.Equal("release/20241215", result[2]);
        Assert.Equal("release/20241201", result[3]);
        Assert.Equal("release/20240601", result[4]);  // 最舊
    }

    [Fact]
    public void SortReleaseBranchesDescending_WithEmptyList_ShouldReturnEmptyList()
    {
        // Arrange
        var branches = new List<string>();

        // Act
        var result = ReleaseBranchHelper.SortReleaseBranchesDescending(branches);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void SortReleaseBranchesDescending_WithNoReleaseBranches_ShouldReturnEmptyList()
    {
        // Arrange
        var branches = new List<string> { "main", "develop", "feature/test" };

        // Act
        var result = ReleaseBranchHelper.SortReleaseBranchesDescending(branches);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void FindNextNewerReleaseBranch_WhenCurrentBranchIsNotLatest_ShouldReturnNextNewerBranch()
    {
        // Arrange
        var currentBranch = "release/20241201";
        var allBranches = new List<string>
        {
            "release/20250101",
            "release/20241201",
            "release/20250315",
            "release/20240601",
            "release/20241215"
        };

        // Act
        var result = ReleaseBranchHelper.FindNextNewerReleaseBranch(currentBranch, allBranches);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("release/20241215", result);  // 下一個較新的版本
    }

    [Fact]
    public void FindNextNewerReleaseBranch_WhenCurrentBranchIsLatest_ShouldReturnNull()
    {
        // Arrange
        var currentBranch = "release/20250315";
        var allBranches = new List<string>
        {
            "release/20250101",
            "release/20241201",
            "release/20250315",
            "release/20240601"
        };

        // Act
        var result = ReleaseBranchHelper.FindNextNewerReleaseBranch(currentBranch, allBranches);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void FindNextNewerReleaseBranch_WithInvalidCurrentBranch_ShouldReturnNull()
    {
        // Arrange
        var currentBranch = "feature/test";
        var allBranches = new List<string>
        {
            "release/20250101",
            "release/20241201"
        };

        // Act
        var result = ReleaseBranchHelper.FindNextNewerReleaseBranch(currentBranch, allBranches);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void FindNextNewerReleaseBranch_WithNullCurrentBranch_ShouldReturnNull()
    {
        // Arrange
        var allBranches = new List<string> { "release/20250101" };

        // Act
        var result = ReleaseBranchHelper.FindNextNewerReleaseBranch(null, allBranches);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void IsLatestReleaseBranch_WhenBranchIsLatest_ShouldReturnTrue()
    {
        // Arrange
        var branchName = "release/20250315";
        var allBranches = new List<string>
        {
            "release/20250101",
            "release/20241201",
            "release/20250315",
            "release/20240601"
        };

        // Act
        var result = ReleaseBranchHelper.IsLatestReleaseBranch(branchName, allBranches);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsLatestReleaseBranch_WhenBranchIsNotLatest_ShouldReturnFalse()
    {
        // Arrange
        var branchName = "release/20241201";
        var allBranches = new List<string>
        {
            "release/20250101",
            "release/20241201",
            "release/20250315"
        };

        // Act
        var result = ReleaseBranchHelper.IsLatestReleaseBranch(branchName, allBranches);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsLatestReleaseBranch_WithInvalidBranch_ShouldReturnFalse()
    {
        // Arrange
        var branchName = "feature/test";
        var allBranches = new List<string> { "release/20250101" };

        // Act
        var result = ReleaseBranchHelper.IsLatestReleaseBranch(branchName, allBranches);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsLatestReleaseBranch_WithNullBranch_ShouldReturnFalse()
    {
        // Arrange
        var allBranches = new List<string> { "release/20250101" };

        // Act
        var result = ReleaseBranchHelper.IsLatestReleaseBranch(null, allBranches);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void FindNextNewerReleaseBranch_WithMultipleNewerBranches_ShouldReturnImmediateNextBranch()
    {
        // Arrange
        var currentBranch = "release/20241201";
        var allBranches = new List<string>
        {
            "release/20250315",  // 最新
            "release/20250101",
            "release/20241215",  // 這個應該被回傳（最接近的較新版本）
            "release/20241201",  // 當前
            "release/20240601"   // 更舊
        };

        // Act
        var result = ReleaseBranchHelper.FindNextNewerReleaseBranch(currentBranch, allBranches);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("release/20241215", result);
    }
}
