using ReleaseKit.Common.Constants;

namespace ReleaseKit.Common.Tests.Constants;

/// <summary>
/// WorkItemTypeConstants 單元測試
/// </summary>
public class WorkItemTypeConstantsTests
{
    [Fact]
    public void UserStoryOrAboveTypes_ShouldContainUserStory()
    {
        // Assert
        Assert.Contains("User Story", WorkItemTypeConstants.UserStoryOrAboveTypes);
    }

    [Fact]
    public void UserStoryOrAboveTypes_ShouldContainFeature()
    {
        // Assert
        Assert.Contains("Feature", WorkItemTypeConstants.UserStoryOrAboveTypes);
    }

    [Fact]
    public void UserStoryOrAboveTypes_ShouldContainEpic()
    {
        // Assert
        Assert.Contains("Epic", WorkItemTypeConstants.UserStoryOrAboveTypes);
    }

    [Fact]
    public void UserStoryOrAboveTypes_ShouldNotContainTask()
    {
        // Assert
        Assert.DoesNotContain("Task", WorkItemTypeConstants.UserStoryOrAboveTypes);
    }

    [Fact]
    public void UserStoryOrAboveTypes_ShouldNotContainBug()
    {
        // Assert
        Assert.DoesNotContain("Bug", WorkItemTypeConstants.UserStoryOrAboveTypes);
    }

    [Fact]
    public void UserStoryOrAboveTypes_ShouldBeCaseInsensitive()
    {
        // Assert
        Assert.Contains("user story", WorkItemTypeConstants.UserStoryOrAboveTypes);
        Assert.Contains("FEATURE", WorkItemTypeConstants.UserStoryOrAboveTypes);
        Assert.Contains("epic", WorkItemTypeConstants.UserStoryOrAboveTypes);
    }

    [Fact]
    public void MaxRecursionDepth_ShouldBeTen()
    {
        // Assert
        Assert.Equal(10, WorkItemTypeConstants.MaxRecursionDepth);
    }
}
