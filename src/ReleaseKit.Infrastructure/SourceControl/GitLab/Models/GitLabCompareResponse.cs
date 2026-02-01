using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.SourceControl.GitLab.Models;

/// <summary>
/// GitLab 分支比較 API 回應模型
/// </summary>
public sealed record GitLabCompareResponse
{
    /// <summary>
    /// Commit 清單
    /// </summary>
    [JsonPropertyName("commits")]
    public List<GitLabCommitResponse> Commits { get; init; } = new();

    /// <summary>
    /// 比較是否逾時
    /// </summary>
    [JsonPropertyName("compare_timeout")]
    public bool CompareTimeout { get; init; }

    /// <summary>
    /// 是否比較相同的 ref
    /// </summary>
    [JsonPropertyName("compare_same_ref")]
    public bool CompareSameRef { get; init; }
}
