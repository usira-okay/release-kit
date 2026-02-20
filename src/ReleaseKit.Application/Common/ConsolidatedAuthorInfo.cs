namespace ReleaseKit.Application.Common;

/// <summary>
/// 整合記錄中的作者資訊
/// </summary>
public sealed record ConsolidatedAuthorInfo
{
    /// <summary>
    /// 作者名稱
    /// </summary>
    public required string AuthorName { get; init; }
}
