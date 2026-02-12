using Microsoft.Extensions.Logging;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 過濾 Pull Request 依使用者的抽象基底任務
/// </summary>
/// <remarks>
/// 封裝從 Redis 讀取 PR 資料、依使用者 ID 清單過濾、並寫回 Redis 的共用邏輯。
/// 子類別需提供來源 Redis Key、目標 Redis Key、平台名稱與使用者 ID 清單。
/// </remarks>
public abstract class BaseFilterPullRequestsByUserTask : ITask
{
    /// <summary>
    /// 日誌記錄器
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Redis 服務
    /// </summary>
    protected readonly IRedisService RedisService;

    /// <summary>
    /// 使用者 ID 與 DisplayName 的對應字典
    /// </summary>
    protected readonly IReadOnlyDictionary<string, string> UserIdToDisplayName;

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="logger">日誌記錄器</param>
    /// <param name="redisService">Redis 服務</param>
    /// <param name="userIdToDisplayName">使用者 ID 與 DisplayName 的對應字典</param>
    protected BaseFilterPullRequestsByUserTask(
        ILogger logger,
        IRedisService redisService,
        IReadOnlyDictionary<string, string> userIdToDisplayName)
    {
        Logger = logger;
        RedisService = redisService;
        UserIdToDisplayName = userIdToDisplayName;
    }

    /// <summary>
    /// 來源 Redis Key（讀取未過濾的 PR 資料）
    /// </summary>
    protected abstract string SourceRedisKey { get; }

    /// <summary>
    /// 目標 Redis Key（寫入過濾後的 PR 資料）
    /// </summary>
    protected abstract string TargetRedisKey { get; }

    /// <summary>
    /// 平台名稱（用於日誌）
    /// </summary>
    protected abstract string PlatformName { get; }

    /// <summary>
    /// 執行過濾任務
    /// </summary>
    public async Task ExecuteAsync()
    {
        Logger.LogInformation("開始過濾 {Platform} PR 資料，依使用者清單過濾", PlatformName);

        // 1. 從 Redis 讀取 PR 資料
        var sourceJson = await RedisService.GetAsync(SourceRedisKey);
        if (string.IsNullOrWhiteSpace(sourceJson))
        {
            Logger.LogWarning("Redis Key {Key} 中無 PR 資料，略過過濾", SourceRedisKey);
            return;
        }

        var fetchResult = sourceJson.ToTypedObject<FetchResult>();
        if (fetchResult == null || fetchResult.Results.Count == 0)
        {
            Logger.LogWarning("無法解析 PR 資料或資料為空，略過過濾");
            return;
        }

        // 2. 檢查使用者清單
        if (UserIdToDisplayName.Count == 0)
        {
            Logger.LogWarning("使用者清單為空，略過過濾");
            return;
        }

        Logger.LogInformation("使用者清單包含 {Count} 個 ID，開始過濾", UserIdToDisplayName.Count);

        // 3. 過濾每個專案的 PR
        var filteredResults = new List<ProjectResult>();
        foreach (var projectResult in fetchResult.Results)
        {
            // 若 ProjectResult 含 Error，保留原樣不過濾
            if (!string.IsNullOrWhiteSpace(projectResult.Error))
            {
                Logger.LogWarning("專案 {Project} 擷取失敗（Error: {Error}），保留原樣不過濾",
                    projectResult.ProjectPath, projectResult.Error);
                filteredResults.Add(projectResult);
                continue;
            }

            // 過濾 PR：保留 AuthorUserId 在使用者字典中的 PR，並將 AuthorName 替換為 DisplayName
            var filteredPRs = projectResult.PullRequests
                .Where(pr => UserIdToDisplayName.ContainsKey(pr.AuthorUserId))
                .Select(pr =>
                {
                    // 若找到對應的 DisplayName，則替換 AuthorName
                    if (UserIdToDisplayName.TryGetValue(pr.AuthorUserId, out var displayName))
                    {
                        return pr with { AuthorName = displayName };
                    }
                    return pr;
                })
                .ToList();

            Logger.LogInformation("專案 {Project} 原有 {Original} 個 PR，過濾後剩餘 {Filtered} 個",
                projectResult.ProjectPath, projectResult.PullRequests.Count, filteredPRs.Count);

            // 建立過濾後的 ProjectResult
            filteredResults.Add(projectResult with { PullRequests = filteredPRs });
        }

        // 4. 建立過濾後的 FetchResult
        var filteredFetchResult = new FetchResult { Results = filteredResults };

        // 5. 寫入目標 Redis Key
        var targetJson = filteredFetchResult.ToJson();
        await RedisService.SetAsync(TargetRedisKey, targetJson);

        Logger.LogInformation("過濾完成，結果已寫入 Redis Key {Key}", TargetRedisKey);

        // 6. 輸出至 stdout
        Console.WriteLine(targetJson);
    }
}
