using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Analysis;

namespace ReleaseKit.Infrastructure.Tests.Analysis;

/// <summary>
/// DependencyInferrer 單元測試
/// </summary>
public class DependencyInferrerTests
{
    private readonly DependencyInferrer _inferrer;

    public DependencyInferrerTests()
    {
        _inferrer = new DependencyInferrer(Mock.Of<ILogger<DependencyInferrer>>());
    }

    private static ProjectStructure CreateProject(string path,
        IReadOnlyList<string>? nugetPkgs = null,
        IReadOnlyList<string>? configKeys = null,
        IReadOnlyList<string>? messageContracts = null)
    {
        return new ProjectStructure
        {
            ProjectPath = path,
            ApiEndpoints = new List<ApiEndpoint>(),
            NuGetPackages = nugetPkgs ?? new List<string>(),
            DbContextFiles = new List<string>(),
            MigrationFiles = new List<string>(),
            MessageContracts = messageContracts ?? new List<string>(),
            ConfigKeys = configKeys ?? new List<string>(),
            InferredDependencies = new List<ServiceDependency>()
        };
    }

    [Fact]
    public void InferSharedNuGetPackages_應找出跨專案共用套件()
    {
        var projects = new List<ProjectStructure>
        {
            CreateProject("project-a", nugetPkgs: new[] { "Newtonsoft.Json", "Serilog" }),
            CreateProject("project-b", nugetPkgs: new[] { "Newtonsoft.Json", "Dapper" })
        };

        var result = DependencyInferrer.InferSharedNuGetPackages(projects[0], projects);

        Assert.Single(result);
        Assert.Equal(DependencyType.NuGet, result[0].DependencyType);
        Assert.Equal("Newtonsoft.Json", result[0].Target);
    }

    [Fact]
    public void InferSharedNuGetPackages_無共用套件時應回傳空清單()
    {
        var projects = new List<ProjectStructure>
        {
            CreateProject("project-a", nugetPkgs: new[] { "PackageA" }),
            CreateProject("project-b", nugetPkgs: new[] { "PackageB" })
        };

        var result = DependencyInferrer.InferSharedNuGetPackages(projects[0], projects);

        Assert.Empty(result);
    }

    [Fact]
    public void InferSharedDatabases_應找出共用ConnectionString()
    {
        var projects = new List<ProjectStructure>
        {
            CreateProject("project-a", configKeys: new[] { "ConnectionStrings:OrderDb", "Logging:Level" }),
            CreateProject("project-b", configKeys: new[] { "ConnectionStrings:OrderDb", "Redis:Host" })
        };

        var result = DependencyInferrer.InferSharedDatabases(projects[0], projects);

        Assert.Single(result);
        Assert.Equal(DependencyType.SharedDb, result[0].DependencyType);
        Assert.Equal("ConnectionStrings:OrderDb", result[0].Target);
    }

    [Fact]
    public void InferSharedMessageQueues_應找出共用訊息契約()
    {
        var projects = new List<ProjectStructure>
        {
            CreateProject("project-a", messageContracts: new[] { "Events/OrderCreatedEvent.cs" }),
            CreateProject("project-b", messageContracts: new[] { "Handlers/OrderCreatedEvent.cs" })
        };

        var result = DependencyInferrer.InferSharedMessageQueues(projects[0], projects);

        Assert.Single(result);
        Assert.Equal(DependencyType.SharedMQ, result[0].DependencyType);
        Assert.Equal("OrderCreatedEvent", result[0].Target);
    }

    [Fact]
    public void InferDependencies_應整合所有推斷結果()
    {
        var projects = new List<ProjectStructure>
        {
            CreateProject("project-a",
                nugetPkgs: new[] { "SharedPkg" },
                configKeys: new[] { "ConnectionStrings:SharedDb" },
                messageContracts: new[] { "Events/OrderEvent.cs" }),
            CreateProject("project-b",
                nugetPkgs: new[] { "SharedPkg" },
                configKeys: new[] { "ConnectionStrings:SharedDb" },
                messageContracts: new[] { "Handlers/OrderEvent.cs" })
        };

        var result = _inferrer.InferDependencies(projects);

        Assert.Equal(2, result.Count);
        // project-a should have 3 dependencies: NuGet, SharedDb, SharedMQ
        Assert.Equal(3, result[0].InferredDependencies.Count);
    }
}
