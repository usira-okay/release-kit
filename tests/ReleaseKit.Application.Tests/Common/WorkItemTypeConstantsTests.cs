using ReleaseKit.Common.Constants;

namespace ReleaseKit.Application.Tests.Common;

/// <summary>
/// WorkItemTypeConstants 單元測試
/// </summary>
public class WorkItemTypeConstantsTests
{
    [Theory]
    [InlineData("User Story", true)]
    [InlineData("Feature", true)]
    [InlineData("Epic", true)]
    [InlineData("Product Backlog Item", true)]
    [InlineData("user story", true)]  // Case insensitive
    [InlineData("FEATURE", true)]
    [InlineData("epic", true)]
    [InlineData("product backlog item", true)]  // Case insensitive
    [InlineData("Bug", false)]
    [InlineData("Task", false)]
    [InlineData("Issue", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsUserStoryLevel_WithVariousTypes_ShouldReturnExpectedResult(string? workItemType, bool expected)
    {
        // Act
        var result = WorkItemTypeConstants.IsUserStoryLevel(workItemType);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void UserStoryLevelTypes_ShouldContainFourTypes()
    {
        // Assert
        Assert.Equal(4, WorkItemTypeConstants.UserStoryLevelTypes.Count);
        Assert.Contains("User Story", WorkItemTypeConstants.UserStoryLevelTypes);
        Assert.Contains("Feature", WorkItemTypeConstants.UserStoryLevelTypes);
        Assert.Contains("Epic", WorkItemTypeConstants.UserStoryLevelTypes);
        Assert.Contains("Product Backlog Item", WorkItemTypeConstants.UserStoryLevelTypes);
    }
}
