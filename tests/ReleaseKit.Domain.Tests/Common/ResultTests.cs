using ReleaseKit.Domain.Common;
using Xunit;

namespace ReleaseKit.Domain.Tests.Common;

/// <summary>
/// Result 類別單元測試
/// </summary>
public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessResult()
    {
        // Arrange & Act
        var result = Result<int>.Success(42);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_ShouldCreateFailureResult()
    {
        // Arrange
        var error = new Error("Test.Error", "測試錯誤");

        // Act
        var result = Result<int>.Failure(error);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(default, result.Value);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Success_WithNullValue_ShouldStillBeSuccess()
    {
        // Arrange & Act
        var result = Result<string?>.Success(null);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public void IsSuccess_WhenErrorIsNull_ShouldReturnTrue()
    {
        // Arrange & Act
        var result = Result<string>.Success("test");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    public void IsFailure_WhenErrorExists_ShouldReturnTrue()
    {
        // Arrange
        var error = new Error("Test.Error", "測試錯誤");

        // Act
        var result = Result<string>.Failure(error);

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
    }
}
