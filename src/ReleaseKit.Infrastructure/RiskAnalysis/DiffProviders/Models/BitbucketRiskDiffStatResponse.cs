using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.RiskAnalysis.DiffProviders.Models;

/// <summary>
/// Bitbucket DiffStat API 回應模型（風險分析用，包含檔案路徑資訊）
/// </summary>
public sealed record BitbucketRiskDiffStatResponse
{
    /// <summary>
    /// 變更的檔案清單
    /// </summary>
    public List<BitbucketRiskDiffStatEntry> Values { get; init; } = new();

    /// <summary>
    /// 下一頁 URL
    /// </summary>
    public string? Next { get; init; }
}

/// <summary>
/// Bitbucket DiffStat 項目（包含檔案路徑）
/// </summary>
public sealed record BitbucketRiskDiffStatEntry
{
    /// <summary>
    /// 項目類型（通常為 "diffstat"）
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// 檔案變更狀態（modified、added、removed、renamed）
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// 新增行數
    /// </summary>
    [JsonPropertyName("lines_added")]
    public int LinesAdded { get; init; }

    /// <summary>
    /// 移除行數
    /// </summary>
    [JsonPropertyName("lines_removed")]
    public int LinesRemoved { get; init; }

    /// <summary>
    /// 變更前的檔案資訊
    /// </summary>
    public BitbucketRiskFileRef? Old { get; init; }

    /// <summary>
    /// 變更後的檔案資訊
    /// </summary>
    public BitbucketRiskFileRef? New { get; init; }
}

/// <summary>
/// Bitbucket 檔案參照（包含路徑）
/// </summary>
public sealed record BitbucketRiskFileRef
{
    /// <summary>
    /// 檔案路徑
    /// </summary>
    public string Path { get; init; } = string.Empty;
}
