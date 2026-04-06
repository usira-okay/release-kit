using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Entities;

/// <summary>
/// PR Diff 上下文資訊
/// </summary>
public sealed record PrDiffContext
{
    /// <summary>PR 標題</summary>
    public required string Title { get; init; }

    /// <summary>PR 描述</summary>
    public string? Description { get; init; }

    /// <summary>來源分支</summary>
    public required string SourceBranch { get; init; }

    /// <summary>目標分支</summary>
    public required string TargetBranch { get; init; }

    /// <summary>作者</summary>
    public required string AuthorName { get; init; }

    /// <summary>PR URL</summary>
    public required string PrUrl { get; init; }

    /// <summary>Git diff 內容</summary>
    public required string DiffContent { get; init; }

    /// <summary>異動的檔案清單</summary>
    public required IReadOnlyList<string> ChangedFiles { get; init; }

    /// <summary>所屬平台</summary>
    public required SourceControlPlatform Platform { get; init; }
}
