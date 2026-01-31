using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.SourceControl.GitLab.Models;

/// <summary>
/// GitLab 分支 API 回應模型
/// </summary>
public sealed record GitLabBranchResponse
{
    /// <summary>
    /// 分支名稱
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Commit 資訊
    /// </summary>
    [JsonPropertyName("commit")]
    public GitLabCommitResponse? Commit { get; init; }

    /// <summary>
    /// 是否為預設分支
    /// </summary>
    [JsonPropertyName("default")]
    public bool Default { get; init; }

    /// <summary>
    /// 是否受保護
    /// </summary>
    [JsonPropertyName("protected")]
    public bool Protected { get; init; }
}
