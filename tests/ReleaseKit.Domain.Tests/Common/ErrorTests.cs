using ReleaseKit.Domain.Common;
using Xunit;

namespace ReleaseKit.Domain.Tests.Common;

/// <summary>
/// Error 類別單元測試
/// </summary>
public class ErrorTests
{
    [Fact]
    public void Constructor_ShouldCreateError()
    {
        // Arrange & Act
        var error = new Error("Test.Error", "測試錯誤訊息");

        // Assert
        Assert.Equal("Test.Error", error.Code);
        Assert.Equal("測試錯誤訊息", error.Message);
    }

    [Fact]
    public void None_ShouldReturnEmptyError()
    {
        // Arrange & Act
        var error = Error.None;

        // Assert
        Assert.Equal(string.Empty, error.Code);
        Assert.Equal(string.Empty, error.Message);
    }

    [Fact]
    public void BranchNotFound_ShouldCreateError()
    {
        // Arrange & Act
        var error = Error.SourceControl.BranchNotFound("main");

        // Assert
        Assert.Equal("SourceControl.BranchNotFound", error.Code);
        Assert.Contains("main", error.Message);
        Assert.Contains("不存在", error.Message);
    }

    [Fact]
    public void ApiError_ShouldCreateError()
    {
        // Arrange & Act
        var error = Error.SourceControl.ApiError("連線逾時");

        // Assert
        Assert.Equal("SourceControl.ApiError", error.Code);
        Assert.Contains("連線逾時", error.Message);
    }

    [Fact]
    public void Unauthorized_ShouldCreateError()
    {
        // Arrange & Act
        var error = Error.SourceControl.Unauthorized;

        // Assert
        Assert.Equal("SourceControl.Unauthorized", error.Code);
        Assert.Contains("驗證失敗", error.Message);
    }

    [Fact]
    public void RateLimitExceeded_ShouldCreateError()
    {
        // Arrange & Act
        var error = Error.SourceControl.RateLimitExceeded;

        // Assert
        Assert.Equal("SourceControl.RateLimitExceeded", error.Code);
        Assert.Contains("請求限制", error.Message);
    }

    [Fact]
    public void NetworkError_ShouldCreateError()
    {
        // Arrange & Act
        var error = Error.SourceControl.NetworkError;

        // Assert
        Assert.Equal("SourceControl.NetworkError", error.Code);
        Assert.Contains("網路", error.Message);
    }

    [Fact]
    public void InvalidResponse_ShouldCreateError()
    {
        // Arrange & Act
        var error = Error.SourceControl.InvalidResponse;

        // Assert
        Assert.Equal("SourceControl.InvalidResponse", error.Code);
        Assert.Contains("無效", error.Message);
    }

    [Fact]
    public void ProjectNotFound_ShouldCreateError()
    {
        // Arrange & Act
        var error = Error.SourceControl.ProjectNotFound("mygroup/myproject");

        // Assert
        Assert.Equal("SourceControl.ProjectNotFound", error.Code);
        Assert.Contains("mygroup/myproject", error.Message);
        Assert.Contains("不存在", error.Message);
    }
}
