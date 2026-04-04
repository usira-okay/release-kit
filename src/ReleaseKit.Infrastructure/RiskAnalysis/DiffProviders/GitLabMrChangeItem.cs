using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.RiskAnalysis.DiffProviders;

/// <summary>
/// GitLab MR 單一檔案變更項目
/// </summary>
public sealed record GitLabMrChangeItem
{
    /// <summary>舊檔案路徑</summary>
    [JsonPropertyName("old_path")]
    public string OldPath { get; init; } = string.Empty;

    /// <summary>新檔案路徑</summary>
    [JsonPropertyName("new_path")]
    public string NewPath { get; init; } = string.Empty;

    /// <summary>是否為新增檔案</summary>
    [JsonPropertyName("new_file")]
    public bool NewFile { get; init; }

    /// <summary>是否為刪除檔案</summary>
    [JsonPropertyName("deleted_file")]
    public bool DeletedFile { get; init; }

    /// <summary>Diff patch 內容</summary>
    [JsonPropertyName("diff")]
    public string Diff { get; init; } = string.Empty;
}
