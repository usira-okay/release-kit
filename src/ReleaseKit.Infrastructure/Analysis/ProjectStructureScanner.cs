using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Infrastructure.Analysis;

/// <summary>
/// 靜態專案結構掃描器，分析本地 .NET 專案目錄結構
/// </summary>
public class ProjectStructureScanner
{
    private readonly ILogger<ProjectStructureScanner> _logger;

    /// <summary>
    /// 初始化 ProjectStructureScanner
    /// </summary>
    public ProjectStructureScanner(ILogger<ProjectStructureScanner> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 掃描指定路徑的專案結構
    /// </summary>
    /// <param name="projectPath">專案識別路徑（如 mygroup/backend-api）</param>
    /// <param name="localPath">本地專案目錄路徑</param>
    /// <returns>專案結構分析結果</returns>
    public ProjectStructure Scan(string projectPath, string localPath)
    {
        _logger.LogInformation("開始掃描專案結構: {ProjectPath} ({LocalPath})", projectPath, localPath);

        return new ProjectStructure
        {
            ProjectPath = projectPath,
            ApiEndpoints = ScanApiEndpoints(localPath),
            NuGetPackages = ScanNuGetPackages(localPath),
            DbContextFiles = ScanDbContextFiles(localPath),
            MigrationFiles = ScanMigrationFiles(localPath),
            MessageContracts = ScanMessageContracts(localPath),
            ConfigKeys = ScanConfigKeys(localPath),
            InferredDependencies = new List<ServiceDependency>()
        };
    }

    /// <summary>
    /// 掃描 .csproj 檔案取得 NuGet 套件引用
    /// </summary>
    internal List<string> ScanNuGetPackages(string localPath)
    {
        var packages = new HashSet<string>();
        foreach (var csproj in Directory.GetFiles(localPath, "*.csproj", SearchOption.AllDirectories))
        {
            var doc = XDocument.Load(csproj);
            var refs = doc.Descendants("PackageReference")
                .Select(e => e.Attribute("Include")?.Value)
                .Where(v => v != null);
            foreach (var pkg in refs)
                packages.Add(pkg!);
        }
        return packages.OrderBy(p => p).ToList();
    }

    /// <summary>
    /// 掃描 Controller 檔案取得 API 端點
    /// </summary>
    internal List<ApiEndpoint> ScanApiEndpoints(string localPath)
    {
        var endpoints = new List<ApiEndpoint>();
        var controllerFiles = Directory.GetFiles(localPath, "*Controller.cs", SearchOption.AllDirectories);

        foreach (var file in controllerFiles)
        {
            var content = File.ReadAllText(file);
            var controllerName = Path.GetFileNameWithoutExtension(file);

            var routeMatch = Regex.Match(content, @"\[Route\(""([^""]+)""\)\]");
            var baseRoute = routeMatch.Success ? routeMatch.Groups[1].Value : "";

            var httpPattern = new Regex(
                @"\[(Http(?:Get|Post|Put|Delete|Patch))(?:\(""([^""]*)""\))?\]\s*(?:\[.*?\]\s*)*public\s+\S+\s+(\w+)",
                RegexOptions.Multiline);

            foreach (Match match in httpPattern.Matches(content))
            {
                var httpMethod = match.Groups[1].Value.Replace("Http", "").ToUpperInvariant();
                var actionRoute = match.Groups[2].Value;
                var actionName = match.Groups[3].Value;
                var fullRoute = string.IsNullOrEmpty(actionRoute)
                    ? baseRoute
                    : $"{baseRoute}/{actionRoute}".TrimStart('/');

                endpoints.Add(new ApiEndpoint
                {
                    HttpMethod = httpMethod,
                    Route = fullRoute,
                    ControllerName = controllerName,
                    ActionName = actionName
                });
            }
        }

        return endpoints;
    }

    /// <summary>
    /// 掃描 DbContext 檔案，回傳相對於 localPath 的路徑
    /// </summary>
    internal List<string> ScanDbContextFiles(string localPath)
    {
        return Directory.GetFiles(localPath, "*DbContext.cs", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(localPath, f))
            .OrderBy(f => f)
            .ToList();
    }

    /// <summary>
    /// 掃描 Migrations 目錄下的 C# 檔案，回傳相對路徑
    /// </summary>
    internal List<string> ScanMigrationFiles(string localPath)
    {
        return Directory.GetFiles(localPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => f.Contains("Migrations" + Path.DirectorySeparatorChar) ||
                        f.Contains("Migrations/"))
            .Select(f => Path.GetRelativePath(localPath, f))
            .OrderBy(f => f)
            .ToList();
    }

    /// <summary>
    /// 掃描訊息契約檔案（Event、Message、Command），回傳相對路徑
    /// </summary>
    internal List<string> ScanMessageContracts(string localPath)
    {
        var patterns = new[] { "*Event.cs", "*Message.cs", "*Command.cs" };
        var files = new List<string>();

        foreach (var pattern in patterns)
        {
            files.AddRange(
                Directory.GetFiles(localPath, pattern, SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(localPath, f)));
        }

        return files.Distinct().OrderBy(f => f).ToList();
    }

    /// <summary>
    /// 掃描 appsettings*.json 並擷取所有設定 key 路徑（以冒號分隔）
    /// </summary>
    internal List<string> ScanConfigKeys(string localPath)
    {
        var keys = new HashSet<string>();
        foreach (var file in Directory.GetFiles(localPath, "appsettings*.json", SearchOption.AllDirectories))
        {
            var json = File.ReadAllText(file);
            var doc = JsonDocument.Parse(json);
            ExtractJsonKeys(doc.RootElement, "", keys);
        }
        return keys.OrderBy(k => k).ToList();
    }

    /// <summary>
    /// 遞迴擷取 JSON 元素的所有葉節點 key 路徑
    /// </summary>
    private static void ExtractJsonKeys(JsonElement element, string prefix, HashSet<string> keys)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return;

        foreach (var prop in element.EnumerateObject())
        {
            var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}:{prop.Name}";
            if (prop.Value.ValueKind == JsonValueKind.Object)
                ExtractJsonKeys(prop.Value, key, keys);
            else
                keys.Add(key);
        }
    }
}
