using ReleaseKit.Infrastructure.AzureDevOps.Mappers;
using ReleaseKit.Infrastructure.AzureDevOps.Models;

namespace ReleaseKit.Infrastructure.Tests.AzureDevOps.Mappers;

/// <summary>
/// AzureDevOpsWorkItemMapper 單元測試
/// </summary>
public class AzureDevOpsWorkItemMapperTests
{
    [Fact]
    public void ToDomain_WithParentRelation_ShouldSetParentWorkItemId()
    {
        // Arrange
        var response = new AzureDevOpsWorkItemResponse
        {
            Id = 100,
            Fields = new Dictionary<string, object?>
            {
                { "System.Title", "子任務" },
                { "System.WorkItemType", "Task" },
                { "System.State", "Active" },
                { "System.AreaPath", "MyProject\\MyTeam" }
            },
            Links = new AzureDevOpsLinksResponse
            {
                Html = new AzureDevOpsLinkResponse
                {
                    Href = "https://dev.azure.com/org/project/_workitems/edit/100"
                }
            },
            Relations = new List<AzureDevOpsRelationResponse>
            {
                new AzureDevOpsRelationResponse
                {
                    Rel = "System.LinkTypes.Hierarchy-Reverse",
                    Url = "https://dev.azure.com/org/_apis/wit/workItems/200"
                }
            }
        };

        // Act
        var workItem = AzureDevOpsWorkItemMapper.ToDomain(response);

        // Assert
        Assert.Equal(200, workItem.ParentWorkItemId);
    }

    [Fact]
    public void ToDomain_WithNoRelations_ShouldSetParentWorkItemIdToNull()
    {
        // Arrange
        var response = new AzureDevOpsWorkItemResponse
        {
            Id = 100,
            Fields = new Dictionary<string, object?>
            {
                { "System.Title", "獨立任務" },
                { "System.WorkItemType", "User Story" },
                { "System.State", "New" },
                { "System.AreaPath", "MyProject" }
            },
            Links = new AzureDevOpsLinksResponse
            {
                Html = new AzureDevOpsLinkResponse { Href = "https://example.com" }
            },
            Relations = null
        };

        // Act
        var workItem = AzureDevOpsWorkItemMapper.ToDomain(response);

        // Assert
        Assert.Null(workItem.ParentWorkItemId);
    }

    [Fact]
    public void ToDomain_WithNonParentRelations_ShouldSetParentWorkItemIdToNull()
    {
        // Arrange
        var response = new AzureDevOpsWorkItemResponse
        {
            Id = 100,
            Fields = new Dictionary<string, object?>
            {
                { "System.Title", "任務" },
                { "System.WorkItemType", "Task" },
                { "System.State", "Active" },
                { "System.AreaPath", "MyProject" }
            },
            Links = new AzureDevOpsLinksResponse
            {
                Html = new AzureDevOpsLinkResponse { Href = "https://example.com" }
            },
            Relations = new List<AzureDevOpsRelationResponse>
            {
                new AzureDevOpsRelationResponse
                {
                    Rel = "System.LinkTypes.Hierarchy-Forward",
                    Url = "https://dev.azure.com/org/_apis/wit/workItems/300"
                }
            }
        };

        // Act
        var workItem = AzureDevOpsWorkItemMapper.ToDomain(response);

        // Assert
        Assert.Null(workItem.ParentWorkItemId);
    }
}
