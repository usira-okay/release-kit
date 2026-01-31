namespace ReleaseKit.Application.Common;

/// <summary>
/// 批次擷取作業的最終輸出結果
/// </summary>
/// <remarks>
/// 包含所有專案的 PR 資訊擷取結果，用於序列化為 JSON 輸出。
/// </remarks>
public sealed record FetchResult
{
    /// <summary>
    /// 各專案的擷取結果清單
    /// </summary>
    public List<ProjectResult> Results { get; init; } = new();
}
