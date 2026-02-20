namespace ReleaseKit.Application.Common;

/// <summary>
/// PR 資訊 DTO
/// </summary>
public sealed record ConsolidatedPrInfo
{
    /// <summary>
    /// PR 網址
    /// </summary>
    public required string Url { get; init; }
}
