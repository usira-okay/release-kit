namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 專案結構分析結果
/// </summary>
public sealed record ProjectStructure
{
    /// <summary>
    /// 專案路徑
    /// </summary>
    public required string ProjectPath { get; init; }

    /// <summary>
    /// API 端點清單
    /// </summary>
    public required IReadOnlyList<ApiEndpoint> ApiEndpoints { get; init; }

    /// <summary>
    /// NuGet 套件引用清單
    /// </summary>
    public required IReadOnlyList<string> NuGetPackages { get; init; }

    /// <summary>
    /// DbContext 檔案清單
    /// </summary>
    public required IReadOnlyList<string> DbContextFiles { get; init; }

    /// <summary>
    /// Migration 檔案清單
    /// </summary>
    public required IReadOnlyList<string> MigrationFiles { get; init; }

    /// <summary>
    /// 訊息契約檔案清單
    /// </summary>
    public required IReadOnlyList<string> MessageContracts { get; init; }

    /// <summary>
    /// 設定檔 Key 清單
    /// </summary>
    public required IReadOnlyList<string> ConfigKeys { get; init; }

    /// <summary>
    /// 推斷的服務相依性清單
    /// </summary>
    public required IReadOnlyList<ServiceDependency> InferredDependencies { get; init; }
}
