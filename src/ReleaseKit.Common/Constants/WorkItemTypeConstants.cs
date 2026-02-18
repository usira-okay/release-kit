namespace ReleaseKit.Common.Constants;

/// <summary>
/// Work Item 類型常數
/// </summary>
public static class WorkItemTypeConstants
{
    /// <summary>
    /// User Story 層級的 Work Item 類型（不區分大小寫）
    /// </summary>
    public static readonly HashSet<string> UserStoryLevelTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "User Story",
        "Feature",
        "Epic"
    };

    /// <summary>
    /// 判斷指定的 Work Item 類型是否為 User Story 層級
    /// </summary>
    /// <param name="workItemType">Work Item 類型</param>
    /// <returns>若為 User Story 層級則回傳 true，否則回傳 false</returns>
    public static bool IsUserStoryLevel(string? workItemType)
    {
        if (string.IsNullOrWhiteSpace(workItemType))
        {
            return false;
        }

        return UserStoryLevelTypes.Contains(workItemType);
    }
}
