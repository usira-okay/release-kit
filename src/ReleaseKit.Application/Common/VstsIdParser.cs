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
    private static readonly Regex VstsRegex = new(@"VSTS(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
        return ParseFromText(sourceBranch);
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
        return ParseFromText(title);
    }

    /// <summary>
    /// 從 PR/MR 資訊解析 VSTS ID（僅在 SourceBranch 為空或 null 時才從 Title 解析）
    /// </summary>
    /// <param name="sourceBranch">來源分支名稱（例如：feature/VSTS12345-add-login）</param>
    /// <param name="title">PR/MR 標題（例如：VSTS12345 新增登入功能）</param>
    /// <returns>解析成功返回 Work Item ID；失敗返回 null</returns>
    /// <remarks>
    /// 解析邏輯：
    /// 1. 若 SourceBranch 不為空，則從 SourceBranch 解析 VSTS ID（無論是否包含 ID）
    /// 2. 若 SourceBranch 為空或 null，則從 Title 解析
    /// 3. 若無法解析，返回 null
    /// </remarks>
    public static int? Parse(string? sourceBranch, string? title)
    {
        // 若 SourceBranch 不為空，優先使用 SourceBranch（即使沒有 VSTS ID）
        if (!string.IsNullOrWhiteSpace(sourceBranch))
        {
            return ParseFromSourceBranch(sourceBranch);
        }

        // 僅在 SourceBranch 為空或 null 時，才從 Title 解析
        return ParseFromTitle(title);
    }

    /// <summary>
    /// 從文字中解析 VSTS ID（私有共用方法）
    /// </summary>
    /// <param name="text">要解析的文字</param>
    /// <returns>解析成功返回 Work Item ID；失敗返回 null</returns>
    private static int? ParseFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = VstsRegex.Match(text);
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
}
