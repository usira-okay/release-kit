using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Tests.Entities;

/// <summary>
/// ProjectStructure 實體單元測試
/// </summary>
public class ProjectStructureTests
{
    [Fact]
    public void ProjectStructure_應正確建立含完整掃描結果()
    {
        var structure = new ProjectStructure
        {
            ProjectPath = "mygroup/backend-api",
            ApiEndpoints = new List<ApiEndpoint>
            {
                new()
                {
                    HttpMethod = "GET",
                    Route = "/api/v1/users",
                    ControllerName = "UserController",
                    ActionName = "GetUsers"
                }
            },
            NuGetPackages = new List<string> { "Newtonsoft.Json" },
            DbContextFiles = new List<string> { "Data/AppDbContext.cs" },
            MigrationFiles = new List<string> { "Migrations/20260101_Init.cs" },
            MessageContracts = new List<string> { "Events/OrderCreatedEvent.cs" },
            ConfigKeys = new List<string> { "ConnectionStrings:DefaultConnection" },
            InferredDependencies = new List<ServiceDependency>
            {
                new()
                {
                    DependencyType = DependencyType.SharedDb,
                    Target = "OrderDB"
                }
            }
        };

        Assert.Equal("mygroup/backend-api", structure.ProjectPath);
        Assert.Single(structure.ApiEndpoints);
        Assert.Equal("GET", structure.ApiEndpoints[0].HttpMethod);
        Assert.Single(structure.InferredDependencies);
        Assert.Equal(DependencyType.SharedDb, structure.InferredDependencies[0].DependencyType);
    }
}
