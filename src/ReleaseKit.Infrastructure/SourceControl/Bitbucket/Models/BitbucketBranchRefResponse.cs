using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.SourceControl.Bitbucket.Models;

/// <summary>
/// Bitbucket 分支參考 API 回應模型
/// </summary>
public sealed record BitbucketBranchRefResponse
{
    /// <summary>
    /// 分支資訊
    /// </summary>
    [JsonPropertyName("branch")]
    public BitbucketBranchResponse Branch { get; init; } = new();
}
