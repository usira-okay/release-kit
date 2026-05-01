using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis;

/// <summary>
/// Expert Agent 工具集工廠
/// </summary>
public class ExpertToolFactory
{
    private readonly IGitOperationService _gitService;
    private readonly ILogger<ExpertToolFactory> _logger;

    /// <summary>
    /// 初始化 <see cref="ExpertToolFactory"/>
    /// </summary>
    public ExpertToolFactory(IGitOperationService gitService, ILogger<ExpertToolFactory> logger)
    {
        _gitService = gitService;
        _logger = logger;
    }

    /// <summary>
    /// 建立 Expert Agent 的工具集
    /// </summary>
    /// <param name="localPath">本地 repo 路徑</param>
    /// <param name="ct">取消標記</param>
    /// <returns>AI 工具清單</returns>
    public IReadOnlyList<AIFunction> CreateTools(string localPath, CancellationToken ct)
    {
        var getCommitOverview = AIFunctionFactory.Create(
            async ([Description("要查看的 CommitSha")] string commitSha) =>
            {
                var result = await _gitService.GetCommitStatAsync(localPath, commitSha, ct);
                if (!result.IsSuccess)
                    return $"錯誤: {result.Error!.Message}";

                var summary = result.Value!;
                var sb = new StringBuilder();
                sb.AppendLine($"CommitSha: {commitSha}");
                sb.AppendLine($"統計: {summary.TotalFilesChanged} 檔案, +{summary.TotalLinesAdded}/-{summary.TotalLinesRemoved}");
                sb.AppendLine("異動檔案:");
                foreach (var f in summary.ChangedFiles)
                    sb.AppendLine($"  [{f.ChangeType}] {f.FilePath}");
                return sb.ToString();
            },
            "get_commit_overview",
            "取得 Commit 的異動摘要（檔案清單 + 行數統計），用於快速評估是否需要看完整 diff");

        var getFullDiff = AIFunctionFactory.Create(
            async ([Description("要取得完整 diff 的 CommitSha")] string commitSha) =>
            {
                var result = await _gitService.GetCommitRawDiffAsync(localPath, commitSha, ct);
                return result.IsSuccess ? result.Value! : $"錯誤: {result.Error!.Message}";
            },
            "get_full_diff",
            "取得指定 CommitSha 的完整 unified diff 內容（較大，建議先用 get_commit_overview 評估）");

        var getFileContent = AIFunctionFactory.Create(
            async ([Description("檔案的相對路徑（相對於 repo 根目錄）")] string filePath) =>
            {
                var fullPath = Path.Combine(localPath, filePath);
                if (!File.Exists(fullPath))
                    return $"檔案不存在: {filePath}";

                var content = await File.ReadAllTextAsync(fullPath, ct);
                // 限制檔案大小避免 token 爆量
                if (content.Length > 50000)
                    return content[..50000] + "\n\n... (檔案過大，已截斷至前 50000 字元)";
                return content;
            },
            "get_file_content",
            "取得指定檔案的完整內容（用於理解變更的上下文）");

        var searchPattern = AIFunctionFactory.Create(
            async (
                [Description("搜尋模式（正規表示式）")] string pattern,
                [Description("檔案 glob 篩選（如 '*.cs'），可省略表示搜尋所有檔案")] string? fileGlob) =>
            {
                var result = await _gitService.SearchPatternAsync(localPath, pattern, fileGlob, ct);
                if (!result.IsSuccess)
                    return $"搜尋失敗: {result.Error!.Message}";

                var output = result.Value ?? string.Empty;
                // 限制輸出大小
                if (output.Length > 20000)
                    return output[..20000] + "\n\n... (結果過多，已截斷)";
                return string.IsNullOrEmpty(output) ? "無符合結果" : output;
            },
            "search_pattern",
            "使用 git grep 搜尋程式碼庫中符合模式的內容（用於尋找呼叫端、消費端等）");

        var listDirectory = AIFunctionFactory.Create(
            ([Description("目錄的相對路徑（相對於 repo 根目錄），空字串表示根目錄")] string path) =>
            {
                var fullPath = Path.Combine(localPath, path ?? string.Empty);
                if (!Directory.Exists(fullPath))
                    return $"目錄不存在: {path}";

                var entries = Directory.GetFileSystemEntries(fullPath)
                    .Select(e => Path.GetRelativePath(localPath, e))
                    .OrderBy(e => e)
                    .ToList();

                return string.Join("\n", entries);
            },
            "list_directory",
            "列出指定目錄的檔案與子目錄結構");

        return [getCommitOverview, getFullDiff, getFileContent, searchPattern, listDirectory];
    }
}
