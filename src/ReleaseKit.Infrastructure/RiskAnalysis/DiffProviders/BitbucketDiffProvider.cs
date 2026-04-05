using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ReleaseKit.Application.Common.RiskAnalysis;
using ReleaseKit.Domain.Common;
using ReleaseKit.Infrastructure.RiskAnalysis.DiffProviders.Models;
using ReleaseKit.Infrastructure.SourceControl.Bitbucket;

namespace ReleaseKit.Infrastructure.RiskAnalysis.DiffProviders;

/// <summary>
/// Bitbucket 平台的 diff 資料提供者
/// </summary>
/// <remarks>
/// 透過 Bitbucket REST API 2.0 取得 PR 的 diffstat（檔案變更統計）與 raw diff（差異內容），
/// 並合併為統一的 <see cref="PullRequestDiff"/> 格式。
/// </remarks>
public class BitbucketDiffProvider : IDiffProvider
{
    private readonly BitbucketRepository _bitbucketRepository;
    private readonly ILogger<BitbucketDiffProvider> _logger;

    /// <summary>
    /// 用於分割 raw diff 中各檔案區段的正規表示式
    /// </summary>
    private static readonly Regex DiffHeaderRegex = new(
        @"^diff --git a/(.+?) b/(.+?)$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="bitbucketRepository">Bitbucket Repository</param>
    /// <param name="logger">日誌記錄器</param>
    public BitbucketDiffProvider(
        BitbucketRepository bitbucketRepository,
        ILogger<BitbucketDiffProvider> logger)
    {
        _bitbucketRepository = bitbucketRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<PullRequestDiff>> GetDiffAsync(string projectPath, string prId)
    {
        // 步驟一：取得 diffstat（檔案層級統計資料）
        var diffStatResult = await _bitbucketRepository.GetPullRequestDiffStatAsync(projectPath, prId);
        if (diffStatResult.IsFailure)
            return Result<PullRequestDiff>.Failure(diffStatResult.Error!);

        var diffStatEntries = diffStatResult.Value!.Values;

        // 若無任何檔案變更，直接回傳空結果
        if (diffStatEntries.Count == 0)
        {
            return Result<PullRequestDiff>.Success(new PullRequestDiff
            {
                PullRequest = null!,
                RepositoryName = ExtractRepositoryName(projectPath),
                Platform = "Bitbucket",
                Files = []
            });
        }

        // 步驟二：取得 raw diff 內容
        var rawDiffResult = await _bitbucketRepository.GetPullRequestRawDiffAsync(projectPath, prId);
        if (rawDiffResult.IsFailure)
            return Result<PullRequestDiff>.Failure(rawDiffResult.Error!);

        var rawDiff = rawDiffResult.Value!;

        // 步驟三：解析 raw diff 並與 diffstat 合併
        var diffContentMap = ParseRawDiff(rawDiff);
        var files = BuildFileDiffs(diffStatEntries, diffContentMap);

        return Result<PullRequestDiff>.Success(new PullRequestDiff
        {
            PullRequest = null!,
            RepositoryName = ExtractRepositoryName(projectPath),
            Platform = "Bitbucket",
            Files = files
        });
    }

    /// <summary>
    /// 解析 raw unified diff，將每個檔案的 diff 內容以路徑為 key 建立字典
    /// </summary>
    private Dictionary<string, string> ParseRawDiff(string rawDiff)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(rawDiff))
            return result;

        var matches = DiffHeaderRegex.Matches(rawDiff);
        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var filePath = match.Groups[2].Value;
            var startIndex = match.Index;
            var endIndex = i + 1 < matches.Count ? matches[i + 1].Index : rawDiff.Length;
            var diffContent = rawDiff[startIndex..endIndex].TrimEnd();
            result[filePath] = diffContent;
        }

        return result;
    }

    /// <summary>
    /// 合併 diffstat 統計與 raw diff 內容，建立 FileDiff 清單
    /// </summary>
    private static List<FileDiff> BuildFileDiffs(
        List<BitbucketRiskDiffStatEntry> entries,
        Dictionary<string, string> diffContentMap)
    {
        var files = new List<FileDiff>(entries.Count);

        foreach (var entry in entries)
        {
            var filePath = entry.New?.Path ?? entry.Old?.Path ?? string.Empty;
            diffContentMap.TryGetValue(filePath, out var diffContent);

            files.Add(new FileDiff
            {
                FilePath = filePath,
                AddedLines = entry.LinesAdded,
                DeletedLines = entry.LinesRemoved,
                DiffContent = diffContent ?? string.Empty,
                IsNewFile = string.Equals(entry.Status, "added", StringComparison.OrdinalIgnoreCase),
                IsDeletedFile = string.Equals(entry.Status, "removed", StringComparison.OrdinalIgnoreCase)
            });
        }

        return files;
    }

    /// <summary>
    /// 從 projectPath（workspace/repo-slug）中擷取 Repository 名稱
    /// </summary>
    private static string ExtractRepositoryName(string projectPath)
    {
        var lastSlash = projectPath.LastIndexOf('/');
        return lastSlash >= 0 ? projectPath[(lastSlash + 1)..] : projectPath;
    }
}
