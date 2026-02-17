namespace ReleaseKit.Application.Common;

/// <summary>
/// User Story 解析結果狀態
/// </summary>
public enum UserStoryResolutionStatus
{
    /// <summary>
    /// 原始 Type 即為 User Story 以上的類型（User Story、Feature、Epic）
    /// </summary>
    AlreadyUserStoryOrAbove = 0,

    /// <summary>
    /// 透過遞迴 Parent 找到 User Story 以上的類型
    /// </summary>
    FoundViaRecursion = 1,

    /// <summary>
    /// 無法找到 User Story 以上的類型（遞迴到頂層、循環參照、超過深度限制）
    /// </summary>
    NotFound = 2,

    /// <summary>
    /// 原始 Work Item 在先前步驟就無法取得資訊
    /// </summary>
    OriginalFetchFailed = 3
}
