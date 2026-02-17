using System.Text.RegularExpressions;

namespace ReleaseKit.Application.Common;

/// <summary>
/// VSTS ID 解析工具
/// </summary>
/// <remarks>
/// 提供從字串中解析 VSTS ID 的共用方法。
/// </remarks>
public static class VstsIdParser
{
    private static readonly Regex VstsRegex = new(@"VSTS(\d+)", RegexOptions.IgnoreCase);

    /// <summary>
    /// 從來源分支名稱解析 VSTS ID
    /// </summary>
    /// <param name="sourceBranch">來源分支名稱（例如：feature/VSTS12345-add-login）</param>
    /// <returns>解析成功返回 Work Item ID；失敗返回 null</returns>
    /// <remarks>
    /// 支援不分大小寫的 VSTS 格式，例如：
    /// - "feature/VSTS12345-add-login" → 12345
    /// - "VSTS99999" → 99999
    /// - "feature/vsts123" → 123（小寫也符合）
    /// - "feature/Vsts456" → 456（混合大小寫也符合）
    /// - "feature/no-id" → null（無 VSTS ID）
    /// </remarks>
    public static int? ParseFromSourceBranch(string? sourceBranch)
    {
        if (string.IsNullOrWhiteSpace(sourceBranch))
        {
            return null;
        }

        var match = VstsRegex.Match(sourceBranch);
        if (!match.Success)
        {
            return null;
        }

        if (int.TryParse(match.Groups[1].Value, out var id))
        {
            return id;
        }

        return null;
    }

    /// <summary>
    /// 從 PR/MR 標題解析 VSTS ID
    /// </summary>
    /// <param name="title">PR/MR 標題（例如：VSTS12345 新增登入功能）</param>
    /// <returns>解析成功返回 Work Item ID；失敗返回 null</returns>
    /// <remarks>
    /// 支援不分大小寫的 VSTS 格式，例如：
    /// - "VSTS12345 新增登入功能" → 12345
    /// - "[VSTS99999] 修復問題" → 99999
    /// - "vsts123: 更新文件" → 123（小寫也符合）
    /// - "新增功能" → null（無 VSTS ID）
    /// </remarks>
    public static int? ParseFromTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var match = VstsRegex.Match(title);
        if (!match.Success)
        {
            return null;
        }

        if (int.TryParse(match.Groups[1].Value, out var id))
        {
            return id;
        }

        return null;
    }

    /// <summary>
    /// 從 PR/MR 資訊解析 VSTS ID（優先從 SourceBranch 解析，若失敗則從 Title 解析）
    /// </summary>
    /// <param name="sourceBranch">來源分支名稱（例如：feature/VSTS12345-add-login）</param>
    /// <param name="title">PR/MR 標題（例如：VSTS12345 新增登入功能）</param>
    /// <returns>解析成功返回 Work Item ID；失敗返回 null</returns>
    /// <remarks>
    /// 解析邏輯：
    /// 1. 優先從 SourceBranch 解析 VSTS ID
    /// 2. 若 SourceBranch 為空或未包含 VSTS ID，則從 Title 解析
    /// 3. 若兩者都無法解析，返回 null
    /// </remarks>
    public static int? Parse(string? sourceBranch, string? title)
    {
        // 優先從 SourceBranch 解析
        var workItemId = ParseFromSourceBranch(sourceBranch);
        if (workItemId.HasValue)
        {
            return workItemId;
        }

        // 若 SourceBranch 無法解析，則從 Title 解析
        return ParseFromTitle(title);
    }
}
