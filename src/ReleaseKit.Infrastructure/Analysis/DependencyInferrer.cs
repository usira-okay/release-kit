using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Infrastructure.Analysis;

/// <summary>
/// 推斷跨專案相依性的引擎
/// </summary>
public class DependencyInferrer
{
    private readonly ILogger<DependencyInferrer> _logger;

    /// <summary>
    /// 初始化 DependencyInferrer
    /// </summary>
    public DependencyInferrer(ILogger<DependencyInferrer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 根據多個專案結構推斷相依性
    /// </summary>
    /// <param name="projectStructures">所有專案的結構分析結果</param>
    /// <returns>含相依性的更新後專案結構清單</returns>
    public IReadOnlyList<ProjectStructure> InferDependencies(IReadOnlyList<ProjectStructure> projectStructures)
    {
        _logger.LogInformation("開始推斷 {Count} 個專案的相依性", projectStructures.Count);

        var result = new List<ProjectStructure>();

        foreach (var project in projectStructures)
        {
            var dependencies = new List<ServiceDependency>();

            dependencies.AddRange(InferSharedNuGetPackages(project, projectStructures));
            dependencies.AddRange(InferSharedDatabases(project, projectStructures));
            dependencies.AddRange(InferSharedMessageQueues(project, projectStructures));

            result.Add(project with { InferredDependencies = dependencies });
        }

        return result;
    }

    /// <summary>
    /// 推斷共用 NuGet 套件
    /// </summary>
    internal static List<ServiceDependency> InferSharedNuGetPackages(
        ProjectStructure current, IReadOnlyList<ProjectStructure> allProjects)
    {
        var dependencies = new List<ServiceDependency>();

        foreach (var pkg in current.NuGetPackages)
        {
            var sharedWith = allProjects
                .Where(p => p.ProjectPath != current.ProjectPath && p.NuGetPackages.Contains(pkg))
                .ToList();

            if (sharedWith.Count > 0)
            {
                dependencies.Add(new ServiceDependency
                {
                    DependencyType = DependencyType.NuGet,
                    Target = pkg
                });
            }
        }

        return dependencies;
    }

    /// <summary>
    /// 推斷共用資料庫（從 ConnectionString 設定 key 中提取 DB 名稱）
    /// </summary>
    internal static List<ServiceDependency> InferSharedDatabases(
        ProjectStructure current, IReadOnlyList<ProjectStructure> allProjects)
    {
        var dependencies = new List<ServiceDependency>();
        var currentDbKeys = current.ConfigKeys
            .Where(k => k.StartsWith("ConnectionStrings:", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (currentDbKeys.Count == 0) return dependencies;

        foreach (var otherProject in allProjects.Where(p => p.ProjectPath != current.ProjectPath))
        {
            var otherDbKeys = otherProject.ConfigKeys
                .Where(k => k.StartsWith("ConnectionStrings:", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var sharedKeys = currentDbKeys.Intersect(otherDbKeys).ToList();
            foreach (var key in sharedKeys)
            {
                dependencies.Add(new ServiceDependency
                {
                    DependencyType = DependencyType.SharedDb,
                    Target = key
                });
            }
        }

        return dependencies;
    }

    /// <summary>
    /// 推斷共用訊息佇列（從 Event/Message/Command 檔案名稱推斷）
    /// </summary>
    internal static List<ServiceDependency> InferSharedMessageQueues(
        ProjectStructure current, IReadOnlyList<ProjectStructure> allProjects)
    {
        var dependencies = new List<ServiceDependency>();
        var currentContracts = current.MessageContracts
            .Select(Path.GetFileNameWithoutExtension)
            .ToHashSet();

        if (currentContracts.Count == 0) return dependencies;

        foreach (var otherProject in allProjects.Where(p => p.ProjectPath != current.ProjectPath))
        {
            var otherContracts = otherProject.MessageContracts
                .Select(Path.GetFileNameWithoutExtension)
                .ToHashSet();

            var shared = currentContracts.Intersect(otherContracts).ToList();
            foreach (var contract in shared)
            {
                dependencies.Add(new ServiceDependency
                {
                    DependencyType = DependencyType.SharedMQ,
                    Target = contract!
                });
            }
        }

        return dependencies;
    }
}
