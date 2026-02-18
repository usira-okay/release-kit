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
    [InlineData("user story", true)]  // Case insensitive
    [InlineData("FEATURE", true)]
    [InlineData("epic", true)]
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
    public void UserStoryLevelTypes_ShouldContainThreeTypes()
    {
        // Assert
        Assert.Equal(3, WorkItemTypeConstants.UserStoryLevelTypes.Count);
        Assert.Contains("User Story", WorkItemTypeConstants.UserStoryLevelTypes);
        Assert.Contains("Feature", WorkItemTypeConstants.UserStoryLevelTypes);
        Assert.Contains("Epic", WorkItemTypeConstants.UserStoryLevelTypes);
    }
}
