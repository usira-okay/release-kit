using System.Globalization;
using System.Text.RegularExpressions;

namespace ReleaseKit.Domain.Helpers;

/// <summary>
/// Release Branch 處理工具類別
/// </summary>
public static class ReleaseBranchHelper
{
    private static readonly Regex ReleaseBranchPattern = new(@"^release/(\d{8})$", RegexOptions.Compiled);

    /// <summary>
    /// 檢查分支名稱是否符合 release/yyyyMMdd 格式
    /// </summary>
    /// <param name="branchName">分支名稱</param>
    /// <returns>true 表示符合格式，false 表示不符合</returns>
    public static bool IsReleaseBranch(string? branchName)
    {
        if (string.IsNullOrWhiteSpace(branchName))
        {
            return false;
        }

        return ReleaseBranchPattern.IsMatch(branchName);
    }

    /// <summary>
    /// 從 release/yyyyMMdd 格式的分支名稱中解析日期
    /// </summary>
    /// <param name="branchName">分支名稱</param>
    /// <returns>解析成功回傳日期，失敗回傳 null</returns>
    public static DateTimeOffset? ParseReleaseBranchDate(string? branchName)
    {
        if (string.IsNullOrWhiteSpace(branchName))
        {
            return null;
        }

        var match = ReleaseBranchPattern.Match(branchName);
        if (!match.Success)
        {
            return null;
        }

        var dateString = match.Groups[1].Value;
        if (DateTimeOffset.TryParseExact(
            dateString,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var date))
        {
            return date;
        }

        return null;
    }

    /// <summary>
    /// 排序 release branches，從最新到最舊
    /// </summary>
    /// <param name="branches">分支名稱集合</param>
    /// <returns>排序後的分支名稱列表</returns>
    public static List<string> SortReleaseBranchesDescending(IEnumerable<string> branches)
    {
        return branches
            .Where(IsReleaseBranch)
            .OrderByDescending(b =>
            {
                var date = ParseReleaseBranchDate(b);
                return date ?? DateTimeOffset.MinValue;
            })
            .ToList();
    }

    /// <summary>
    /// 找出比給定分支更新的下一個 release branch
    /// </summary>
    /// <param name="currentBranch">當前分支名稱</param>
    /// <param name="allBranches">所有分支名稱集合</param>
    /// <returns>找到回傳下一個較新的分支名稱，未找到回傳 null</returns>
    public static string? FindNextNewerReleaseBranch(string? currentBranch, IEnumerable<string> allBranches)
    {
        if (string.IsNullOrWhiteSpace(currentBranch) || !IsReleaseBranch(currentBranch))
        {
            return null;
        }

        var currentDate = ParseReleaseBranchDate(currentBranch);
        if (!currentDate.HasValue)
        {
            return null;
        }

        var sortedBranches = SortReleaseBranchesDescending(allBranches);
        
        // 找出比當前分支日期更新的所有分支，並回傳最舊的那一個（即下一個較新的版本）
        var newerBranches = sortedBranches
            .Where(b =>
            {
                var date = ParseReleaseBranchDate(b);
                return date.HasValue && date.Value > currentDate.Value;
            })
            .ToList();

        // 回傳日期最接近（即最舊的較新分支）
        return newerBranches.Count > 0 ? newerBranches.Last() : null;
    }

    /// <summary>
    /// 檢查指定的分支是否為所有 release branches 中最新的
    /// </summary>
    /// <param name="branchName">要檢查的分支名稱</param>
    /// <param name="allBranches">所有分支名稱集合</param>
    /// <returns>true 表示是最新的，false 表示不是最新的或不是 release branch</returns>
    public static bool IsLatestReleaseBranch(string? branchName, IEnumerable<string> allBranches)
    {
        if (string.IsNullOrWhiteSpace(branchName) || !IsReleaseBranch(branchName))
        {
            return false;
        }

        var sortedBranches = SortReleaseBranchesDescending(allBranches);
        return sortedBranches.Count > 0 && sortedBranches[0] == branchName;
    }
}
