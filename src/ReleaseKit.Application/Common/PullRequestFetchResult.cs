namespace ReleaseKit.Application.Common;

/// <summary>
/// 拉取 PR 結果模型
/// </summary>
/// <remarks>
/// 結構上與 FetchResult 相同，但使用 Projects 作為屬性名稱而非 Results。
/// 此類型用於測試和特定場景中以區分 PR 清單的含義。
/// 在序列化時轉換為標準 FetchResult 格式進行 Redis 存儲。
/// </remarks>
public sealed record PullRequestFetchResult
{
    /// <summary>
    /// 各專案的擷取結果清單
    /// </summary>
    public List<ProjectResult> Projects { get; init; } = new();
}
