using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 過濾 GitLab Pull Request 依使用者任務
/// </summary>
/// <remarks>
/// 從資料傳遞存放區 `GitLab:PullRequests` 讀取資料，依 UserMapping 的 GitLabUserId 過濾，
/// 將結果寫入資料傳遞存放區 Key `GitLab:PullRequests:ByUser`。
/// </remarks>
public class FilterGitLabPullRequestsByUserTask : BaseFilterPullRequestsByUserTask
{
    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="logger">日誌記錄器</param>
    /// <param name="dataTransferService">資料傳遞服務</param>
    /// <param name="userMappingOptions">使用者對應設定</param>
    public FilterGitLabPullRequestsByUserTask(
        ILogger<FilterGitLabPullRequestsByUserTask> logger,
        IDataTransferService dataTransferService,
        IOptions<UserMappingOptions> userMappingOptions)
        : base(
            logger,
            dataTransferService,
            ExtractGitLabUserIdToDisplayName(userMappingOptions.Value))
    {
    }

    /// <inheritdoc />
    protected override string SourceGroupKey => DataTransferKeys.GitLabHash;

    /// <inheritdoc />
    protected override string SourceGroupField => DataTransferKeys.Fields.PullRequests;

    /// <inheritdoc />
    protected override string TargetGroupKey => DataTransferKeys.GitLabHash;

    /// <inheritdoc />
    protected override string TargetGroupField => DataTransferKeys.Fields.PullRequestsByUser;

    /// <inheritdoc />
    protected override string PlatformName => "GitLab";

    /// <summary>
    /// 從 UserMappingOptions 中提取 GitLab 使用者 ID 與 DisplayName 的對應字典
    /// </summary>
    /// <param name="options">使用者對應設定</param>
    /// <returns>GitLab 使用者 ID 與 DisplayName 的對應字典</returns>
    private static IReadOnlyDictionary<string, string> ExtractGitLabUserIdToDisplayName(UserMappingOptions options)
    {
        return options.Mappings
            .Where(m => !string.IsNullOrWhiteSpace(m.GitLabUserId) && !string.IsNullOrWhiteSpace(m.DisplayName))
            .GroupBy(m => m.GitLabUserId)
            .ToDictionary(g => g.Key, g => g.First().DisplayName);
    }
}
