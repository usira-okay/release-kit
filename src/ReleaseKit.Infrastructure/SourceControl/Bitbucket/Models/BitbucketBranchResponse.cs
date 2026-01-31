using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.SourceControl.Bitbucket.Models;

/// <summary>
/// Bitbucket 分支 API 回應模型
/// </summary>
public sealed record BitbucketBranchResponse
{
    /// <summary>
    /// 分支名稱
    /// </summary>
    public string Name { get; init; } = string.Empty;
}
