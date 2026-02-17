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
}
