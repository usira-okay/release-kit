using ReleaseKit.Infrastructure.AzureDevOps.Mappers;
using ReleaseKit.Infrastructure.AzureDevOps.Models;
using Xunit;

namespace ReleaseKit.Infrastructure.Tests.AzureDevOps.Mappers;

/// <summary>
/// AzureDevOpsWorkItemMapper 單元測試
/// </summary>
public class AzureDevOpsWorkItemMapperTests
{
    [Fact]
    public void ExtractParentWorkItemId_WithHierarchyReverseRelation_ShouldReturnParentId()
    {
        // Arrange
        var response = new AzureDevOpsWorkItemResponse
        {
            Id = 123,
            Fields = new Dictionary<string, object?>
            {
                ["System.Title"] = "子項目",
                ["System.WorkItemType"] = "Task",
                ["System.State"] = "Active",
                ["System.AreaPath"] = "MyTeam"
            },
            Relations = new List<AzureDevOpsRelationResponse>
            {
                new()
                {
                    Rel = "System.LinkTypes.Hierarchy-Reverse",
                    Url = "https://dev.azure.com/myorg/_apis/wit/workItems/456"
                }
            }
        };

        // Act
        var parentId = AzureDevOpsWorkItemMapper.ExtractParentWorkItemId(response);

        // Assert
        Assert.Equal(456, parentId);
    }

    [Fact]
    public void ExtractParentWorkItemId_WithNoRelations_ShouldReturnNull()
    {
        // Arrange
        var response = new AzureDevOpsWorkItemResponse
        {
            Id = 123,
            Fields = new Dictionary<string, object?>
            {
                ["System.Title"] = "獨立項目",
                ["System.WorkItemType"] = "User Story",
                ["System.State"] = "Active",
                ["System.AreaPath"] = "MyTeam"
            },
            Relations = null
        };

        // Act
        var parentId = AzureDevOpsWorkItemMapper.ExtractParentWorkItemId(response);

        // Assert
        Assert.Null(parentId);
    }

    [Fact]
    public void ExtractParentWorkItemId_WithMultipleRelations_ShouldReturnOnlyHierarchyReverse()
    {
        // Arrange
        var response = new AzureDevOpsWorkItemResponse
        {
            Id = 123,
            Fields = new Dictionary<string, object?>
            {
                ["System.Title"] = "有多個關聯的項目",
                ["System.WorkItemType"] = "Bug",
                ["System.State"] = "Active",
                ["System.AreaPath"] = "MyTeam"
            },
            Relations = new List<AzureDevOpsRelationResponse>
            {
                new()
                {
                    Rel = "System.LinkTypes.Related",
                    Url = "https://dev.azure.com/myorg/_apis/wit/workItems/999"
                },
                new()
                {
                    Rel = "System.LinkTypes.Hierarchy-Reverse",
                    Url = "https://dev.azure.com/myorg/_apis/wit/workItems/789"
                },
                new()
                {
                    Rel = "System.LinkTypes.Dependency-Forward",
                    Url = "https://dev.azure.com/myorg/_apis/wit/workItems/888"
                }
            }
        };

        // Act
        var parentId = AzureDevOpsWorkItemMapper.ExtractParentWorkItemId(response);

        // Assert
        Assert.Equal(789, parentId);
    }

    [Fact]
    public void ExtractParentWorkItemId_WithInvalidUrlFormat_ShouldReturnNull()
    {
        // Arrange
        var response = new AzureDevOpsWorkItemResponse
        {
            Id = 123,
            Fields = new Dictionary<string, object?>
            {
                ["System.Title"] = "URL 格式異常",
                ["System.WorkItemType"] = "Task",
                ["System.State"] = "Active",
                ["System.AreaPath"] = "MyTeam"
            },
            Relations = new List<AzureDevOpsRelationResponse>
            {
                new()
                {
                    Rel = "System.LinkTypes.Hierarchy-Reverse",
                    Url = "invalid-url-format"
                }
            }
        };

        // Act
        var parentId = AzureDevOpsWorkItemMapper.ExtractParentWorkItemId(response);

        // Assert
        Assert.Null(parentId);
    }

    [Fact]
    public void ExtractParentWorkItemId_WithEmptyRelationsList_ShouldReturnNull()
    {
        // Arrange
        var response = new AzureDevOpsWorkItemResponse
        {
            Id = 123,
            Fields = new Dictionary<string, object?>
            {
                ["System.Title"] = "空關聯清單",
                ["System.WorkItemType"] = "Task",
                ["System.State"] = "Active",
                ["System.AreaPath"] = "MyTeam"
            },
            Relations = new List<AzureDevOpsRelationResponse>()
        };

        // Act
        var parentId = AzureDevOpsWorkItemMapper.ExtractParentWorkItemId(response);

        // Assert
        Assert.Null(parentId);
    }

    [Fact]
    public void ToDomain_ShouldMapBasicFields()
    {
        // Arrange
        var response = new AzureDevOpsWorkItemResponse
        {
            Id = 100,
            Fields = new Dictionary<string, object?>
            {
                ["System.Title"] = "測試工作項目",
                ["System.WorkItemType"] = "Bug",
                ["System.State"] = "Active",
                ["System.AreaPath"] = "MyProject\\MyTeam"
            },
            Links = new AzureDevOpsLinksResponse
            {
                Html = new AzureDevOpsLinkResponse
                {
                    Href = "https://dev.azure.com/myorg/myproject/_workitems/edit/100"
                }
            }
        };

        // Act
        var domain = AzureDevOpsWorkItemMapper.ToDomain(response);

        // Assert
        Assert.Equal(100, domain.WorkItemId);
        Assert.Equal("測試工作項目", domain.Title);
        Assert.Equal("Bug", domain.Type);
        Assert.Equal("Active", domain.State);
        Assert.Equal("https://dev.azure.com/myorg/myproject/_workitems/edit/100", domain.Url);
        Assert.Equal("MyProject\\MyTeam", domain.OriginalTeamName);
    }
}
