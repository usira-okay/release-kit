namespace ReleaseKit.Application.Common;

/// <summary>
/// 作者資訊 DTO
/// </summary>
public sealed record ConsolidatedAuthorInfo
{
    /// <summary>
    /// 作者名稱
    /// </summary>
    public required string AuthorName { get; init; }
}
