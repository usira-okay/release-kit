using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Domain.Tests.Entities;

/// <summary>
/// MergeRequest 實體測試
/// </summary>
public class MergeRequestTests
{
    [Fact]
    public void Constructor_ShouldCreateMergeRequest_WhenAllRequiredPropertiesProvided()
    {
        // Arrange & Act
        var mergeRequest = new MergeRequest
        {
            Id = "123",
            Number = 456,
            Title = "Test MR",
            Description = "Test Description",
            SourceBranch = "feature/test",
            TargetBranch = "main",
            State = "merged",
            Author = "test-user",
            CreatedAt = new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2024, 1, 2, 10, 0, 0, TimeSpan.Zero),
            MergedAt = new DateTimeOffset(2024, 1, 2, 12, 0, 0, TimeSpan.Zero),
            WebUrl = "https://gitlab.com/test/test/-/merge_requests/456"
        };

        // Assert
        Assert.Equal("123", mergeRequest.Id);
        Assert.Equal(456, mergeRequest.Number);
        Assert.Equal("Test MR", mergeRequest.Title);
        Assert.Equal("Test Description", mergeRequest.Description);
        Assert.Equal("feature/test", mergeRequest.SourceBranch);
        Assert.Equal("main", mergeRequest.TargetBranch);
        Assert.Equal("merged", mergeRequest.State);
        Assert.Equal("test-user", mergeRequest.Author);
        Assert.Equal(new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero), mergeRequest.CreatedAt);
        Assert.Equal(new DateTimeOffset(2024, 1, 2, 10, 0, 0, TimeSpan.Zero), mergeRequest.UpdatedAt);
        Assert.Equal(new DateTimeOffset(2024, 1, 2, 12, 0, 0, TimeSpan.Zero), mergeRequest.MergedAt);
        Assert.Equal("https://gitlab.com/test/test/-/merge_requests/456", mergeRequest.WebUrl);
    }

    [Fact]
    public void Constructor_ShouldCreateMergeRequest_WithNullOptionalProperties()
    {
        // Arrange & Act
        var mergeRequest = new MergeRequest
        {
            Id = "123",
            Number = 456,
            Title = "Test MR",
            Description = null,
            SourceBranch = "feature/test",
            TargetBranch = "main",
            State = "opened",
            Author = "test-user",
            CreatedAt = new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2024, 1, 2, 10, 0, 0, TimeSpan.Zero),
            MergedAt = null,
            WebUrl = "https://gitlab.com/test/test/-/merge_requests/456"
        };

        // Assert
        Assert.Null(mergeRequest.Description);
        Assert.Null(mergeRequest.MergedAt);
    }
}
