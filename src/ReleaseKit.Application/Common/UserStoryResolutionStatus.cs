using System.Text.Json.Serialization;

namespace ReleaseKit.Application.Common;

/// <summary>
/// 表示 Work Item 的 User Story 解析結果
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserStoryResolutionStatus
{
    /// <summary>
    /// 原始 Type 就是 User Story 或以上的類型
    /// </summary>
    AlreadyUserStoryOrAbove,
    
    /// <summary>
    /// 透過遞迴找到 User Story 或以上的類型
    /// </summary>
    FoundViaRecursion,
    
    /// <summary>
    /// 無法找到 User Story 或以上的類型
    /// </summary>
    NotFound,
    
    /// <summary>
    /// 原始的 Work Item 就無法取得資訊
    /// </summary>
    OriginalFetchFailed
}
