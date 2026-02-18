using ReleaseKit.Infrastructure.AzureDevOps.Mappers;
using ReleaseKit.Infrastructure.AzureDevOps.Models;
using Xunit;

namespace ReleaseKit.Infrastructure.Tests.AzureDevOps;

/// <summary>
/// AzureDevOpsWorkItemMapper 單元測試
/// </summary>
public class AzureDevOpsWorkItemMapperTests
{
    [Fact]
    public void ToDomain_WithValidResponse_ShouldMapCorrectly()
    {
        // Arrange
        var response = new AzureDevOpsWorkItemResponse
        {
            Id = 12345,
            Fields = new Dictionary<string, object?>
            {
                ["System.Title"] = "修復登入頁面 500 錯誤",
                ["System.WorkItemType"] = "Bug",
                ["System.State"] = "Active",
                ["System.AreaPath"] = "MyProject\\TeamA"
            },
            Links = new AzureDevOpsLinksResponse
            {
                Html = new AzureDevOpsLinkResponse
                {
                    Href = "https://dev.azure.com/org/project/_workitems/edit/12345"
                }
            }
        };

        // Act
        var domain = AzureDevOpsWorkItemMapper.ToDomain(response);

        // Assert
        Assert.Equal(12345, domain.WorkItemId);
        Assert.Equal("修復登入頁面 500 錯誤", domain.Title);
        Assert.Equal("Bug", domain.Type);
        Assert.Equal("Active", domain.State);
        Assert.Equal("https://dev.azure.com/org/project/_workitems/edit/12345", domain.Url);
        Assert.Equal("MyProject\\TeamA", domain.OriginalTeamName);
        Assert.Null(domain.ParentId);
    }

    [Theory]
    [InlineData("https://dev.azure.com/org/project/_apis/wit/workitems/67890", 67890)]
    [InlineData("https://dev.azure.com/org/project/_apis/wit/WorkItems/67890", 67890)]
    [InlineData("https://dev.azure.com/org/project/_apis/wit/WORKITEMS/67890", 67890)]
    [InlineData("https://dev.azure.com/org/project/_apis/wit/workItems/67890", 67890)]
    public void ToDomain_WithParentRelation_ShouldExtractParentId_CaseInsensitive(string url, int expectedParentId)
    {
        // Arrange
        var response = new AzureDevOpsWorkItemResponse
        {
            Id = 12345,
            Fields = new Dictionary<string, object?>
            {
                ["System.Title"] = "子任務",
                ["System.WorkItemType"] = "Task",
                ["System.State"] = "Active",
                ["System.AreaPath"] = "MyProject\\TeamA"
            },
            Relations = new List<AzureDevOpsRelationResponse>
            {
                new AzureDevOpsRelationResponse
                {
                    Rel = "System.LinkTypes.Hierarchy-Reverse",
                    Url = url
                }
            }
        };

        // Act
        var domain = AzureDevOpsWorkItemMapper.ToDomain(response);

        // Assert
        Assert.Equal(expectedParentId, domain.ParentId);
    }

    [Fact]
    public void ToDomain_WithoutParentRelation_ShouldReturnNullParentId()
    {
        // Arrange
        var response = new AzureDevOpsWorkItemResponse
        {
            Id = 12345,
            Fields = new Dictionary<string, object?>
            {
                ["System.Title"] = "獨立任務",
                ["System.WorkItemType"] = "Task",
                ["System.State"] = "Active",
                ["System.AreaPath"] = "MyProject\\TeamA"
            },
            Relations = new List<AzureDevOpsRelationResponse>
            {
                new AzureDevOpsRelationResponse
                {
                    Rel = "System.LinkTypes.Related",
                    Url = "https://dev.azure.com/org/project/_apis/wit/workitems/99999"
                }
            }
        };

        // Act
        var domain = AzureDevOpsWorkItemMapper.ToDomain(response);

        // Assert
        Assert.Null(domain.ParentId);
    }

    [Fact]
    public void ToDomain_WithNullRelations_ShouldReturnNullParentId()
    {
        // Arrange
        var response = new AzureDevOpsWorkItemResponse
        {
            Id = 12345,
            Fields = new Dictionary<string, object?>
            {
                ["System.Title"] = "獨立任務",
                ["System.WorkItemType"] = "Task",
                ["System.State"] = "Active",
                ["System.AreaPath"] = "MyProject\\TeamA"
            },
            Relations = null
        };

        // Act
        var domain = AzureDevOpsWorkItemMapper.ToDomain(response);

        // Assert
        Assert.Null(domain.ParentId);
    }

    [Fact]
    public void ToDomain_WithEmptyRelations_ShouldReturnNullParentId()
    {
        // Arrange
        var response = new AzureDevOpsWorkItemResponse
        {
            Id = 12345,
            Fields = new Dictionary<string, object?>
            {
                ["System.Title"] = "獨立任務",
                ["System.WorkItemType"] = "Task",
                ["System.State"] = "Active",
                ["System.AreaPath"] = "MyProject\\TeamA"
            },
            Relations = new List<AzureDevOpsRelationResponse>()
        };

        // Act
        var domain = AzureDevOpsWorkItemMapper.ToDomain(response);

        // Assert
        Assert.Null(domain.ParentId);
    }
}
