using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.RiskAnalysis.DiffProviders;

/// <summary>
/// GitLab MR Changes API 回應模型
/// </summary>
public sealed record GitLabMrChangesResponse
{
    /// <summary>MR 的檔案變更清單</summary>
    [JsonPropertyName("changes")]
    public List<GitLabMrChangeItem> Changes { get; init; } = new();
}
