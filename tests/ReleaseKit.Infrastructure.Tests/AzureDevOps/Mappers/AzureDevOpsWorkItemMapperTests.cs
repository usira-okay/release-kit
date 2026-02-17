using ReleaseKit.Infrastructure.AzureDevOps.Mappers;
using ReleaseKit.Infrastructure.AzureDevOps.Models;

namespace ReleaseKit.Infrastructure.Tests.AzureDevOps.Mappers;

/// <summary>
/// AzureDevOpsWorkItemMapper 單元測試
/// </summary>
public class AzureDevOpsWorkItemMapperTests
{
    [Fact]
    public void ToDomain_WithParentRelation_ShouldParseParentWorkItemId()
    {
        // Arrange
        var response = new AzureDevOpsWorkItemResponse
        {
            Id = 12345,
            Fields = new Dictionary<string, object?>
            {
                ["System.Title"] = "修復登入頁面 CSS 問題",
                ["System.WorkItemType"] = "Bug",
                ["System.State"] = "Active",
                ["System.AreaPath"] = "TeamA"
            },
            Links = new AzureDevOpsLinksResponse
            {
                Html = new AzureDevOpsLinkResponse
                {
                    Href = "https://dev.azure.com/org/project/_workitems/edit/12345"
                }
            },
            Relations = new List<AzureDevOpsRelationResponse>
            {
                new AzureDevOpsRelationResponse
                {
                    Rel = "System.LinkTypes.Hierarchy-Reverse",
                    Url = "https://dev.azure.com/org/project/_apis/wit/workItems/67890",
                    Attributes = null
                }
            }
        };

        // Act
        var domain = AzureDevOpsWorkItemMapper.ToDomain(response);

        // Assert
        Assert.Equal(67890, domain.ParentWorkItemId);
    }

    [Fact]
    public void ToDomain_WithoutParentRelation_ShouldHaveNullParentWorkItemId()
    {
        // Arrange
        var response = new AzureDevOpsWorkItemResponse
        {
            Id = 12345,
            Fields = new Dictionary<string, object?>
            {
                ["System.Title"] = "實作使用者登入功能",
                ["System.WorkItemType"] = "User Story",
                ["System.State"] = "Active",
                ["System.AreaPath"] = "TeamA"
            },
            Links = new AzureDevOpsLinksResponse
            {
                Html = new AzureDevOpsLinkResponse
                {
                    Href = "https://dev.azure.com/org/project/_workitems/edit/12345"
                }
            },
            Relations = null
        };

        // Act
        var domain = AzureDevOpsWorkItemMapper.ToDomain(response);

        // Assert
        Assert.Null(domain.ParentWorkItemId);
    }

    [Fact]
    public void ToDomain_WithMultipleRelations_ShouldIdentifyParentCorrectly()
    {
        // Arrange
        var response = new AzureDevOpsWorkItemResponse
        {
            Id = 12345,
            Fields = new Dictionary<string, object?>
            {
                ["System.Title"] = "測試任務",
                ["System.WorkItemType"] = "Task",
                ["System.State"] = "Active",
                ["System.AreaPath"] = "TeamA"
            },
            Links = new AzureDevOpsLinksResponse
            {
                Html = new AzureDevOpsLinkResponse
                {
                    Href = "https://dev.azure.com/org/project/_workitems/edit/12345"
                }
            },
            Relations = new List<AzureDevOpsRelationResponse>
            {
                new AzureDevOpsRelationResponse
                {
                    Rel = "System.LinkTypes.Related",
                    Url = "https://dev.azure.com/org/project/_apis/wit/workItems/11111",
                    Attributes = null
                },
                new AzureDevOpsRelationResponse
                {
                    Rel = "System.LinkTypes.Hierarchy-Reverse",
                    Url = "https://dev.azure.com/org/project/_apis/wit/workItems/22222",
                    Attributes = null
                },
                new AzureDevOpsRelationResponse
                {
                    Rel = "Hyperlink",
                    Url = "https://example.com/some-link",
                    Attributes = null
                }
            }
        };

        // Act
        var domain = AzureDevOpsWorkItemMapper.ToDomain(response);

        // Assert
        Assert.Equal(22222, domain.ParentWorkItemId);
    }

    [Fact]
    public void ToDomain_WithEmptyRelationsList_ShouldHaveNullParentWorkItemId()
    {
        // Arrange
        var response = new AzureDevOpsWorkItemResponse
        {
            Id = 12345,
            Fields = new Dictionary<string, object?>
            {
                ["System.Title"] = "測試任務",
                ["System.WorkItemType"] = "Task",
                ["System.State"] = "Active",
                ["System.AreaPath"] = "TeamA"
            },
            Links = new AzureDevOpsLinksResponse
            {
                Html = new AzureDevOpsLinkResponse
                {
                    Href = "https://dev.azure.com/org/project/_workitems/edit/12345"
                }
            },
            Relations = new List<AzureDevOpsRelationResponse>()
        };

        // Act
        var domain = AzureDevOpsWorkItemMapper.ToDomain(response);

        // Assert
        Assert.Null(domain.ParentWorkItemId);
    }
}
