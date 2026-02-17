namespace ReleaseKit.Common.Constants;

/// <summary>
/// Work Item 類型相關常數
/// </summary>
public static class WorkItemTypeConstants
{
    /// <summary>
    /// 視為 User Story 以上層級的類型集合（User Story、Feature、Epic）
    /// </summary>
    public static readonly HashSet<string> UserStoryOrAboveTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "User Story",
        "Feature",
        "Epic"
    };

    /// <summary>
    /// 遞迴查找 Parent 的最大深度限制
    /// </summary>
    public const int MaxRecursionDepth = 10;
}
