using Microsoft.Extensions.Logging;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 過濾 Pull Request 依使用者的抽象基底任務
/// </summary>
/// <remarks>
/// 封裝從 資料交換儲存體 讀取 PR 資料、依使用者 ID 清單過濾、並寫回 資料交換儲存體 的共用邏輯。
/// 子類別需提供來源 資料交換儲存體 Key、目標 資料交換儲存體 Key、平台名稱與使用者 ID 清單。
/// </remarks>
public abstract class BaseFilterPullRequestsByUserTask : ITask
{
    /// <summary>
    /// 日誌記錄器
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// 資料交換儲存體 服務
    /// </summary>
    protected readonly IDataTransferService _dataTransferService;

    /// <summary>
    /// 使用者 ID 與 DisplayName 的對應字典
    /// </summary>
    protected readonly IReadOnlyDictionary<string, string> UserIdToDisplayName;

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="logger">日誌記錄器</param>
    /// <param name="dataTransferService">資料交換儲存體 服務</param>
    /// <param name="userIdToDisplayName">使用者 ID 與 DisplayName 的對應字典</param>
    protected BaseFilterPullRequestsByUserTask(
        ILogger logger,
        IDataTransferService dataTransferService,
        IReadOnlyDictionary<string, string> userIdToDisplayName)
    {
        Logger = logger;
        _dataTransferService = dataTransferService;
        UserIdToDisplayName = userIdToDisplayName;
    }

    /// <summary>
    /// 來源 資料交換儲存體 Hash 鍵值（讀取未過濾的 PR 資料）
    /// </summary>
    protected abstract string SourceDataTransferGroupKey { get; }

    /// <summary>
    /// 來源 資料交換儲存體 Hash 欄位名稱
    /// </summary>
    protected abstract string SourceDataTransferFieldKey { get; }

    /// <summary>
    /// 目標 資料交換儲存體 Hash 鍵值（寫入過濾後的 PR 資料）
    /// </summary>
    protected abstract string TargetDataTransferGroupKey { get; }

    /// <summary>
    /// 目標 資料交換儲存體 Hash 欄位名稱
    /// </summary>
    protected abstract string TargetDataTransferFieldKey { get; }

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

        // 清除目標 資料交換儲存體 資料
        if (await _dataTransferService.FieldExistsAsync(TargetDataTransferGroupKey, TargetDataTransferFieldKey))
        {
            Logger.LogInformation("清除 資料交換儲存體 中的舊資料，Hash: {HashKey} Field: {Field}", TargetDataTransferGroupKey, TargetDataTransferFieldKey);
            await _dataTransferService.DeleteFieldAsync(TargetDataTransferGroupKey, TargetDataTransferFieldKey);
        }

        // 1. 從 資料交換儲存體 讀取 PR 資料
        var sourceJson = await _dataTransferService.GetFieldAsync(SourceDataTransferGroupKey, SourceDataTransferFieldKey);
        if (sourceJson is null)
        {
            Logger.LogError("資料交換儲存體 Hash {HashKey} Field {Field} 中無 PR 資料，請先執行前置指令", SourceDataTransferGroupKey, SourceDataTransferFieldKey);
            throw new InvalidOperationException($"資料交換儲存體 Hash {SourceDataTransferGroupKey} Field {SourceDataTransferFieldKey} 中無 PR 資料");
        }

        if (string.IsNullOrWhiteSpace(sourceJson))
        {
            Logger.LogWarning("資料交換儲存體 Hash {HashKey} Field {Field} 中無 PR 資料，略過過濾", SourceDataTransferGroupKey, SourceDataTransferFieldKey);
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
                .Select(pr => pr with { AuthorName = UserIdToDisplayName[pr.AuthorUserId] })
                .ToList();

            Logger.LogInformation("專案 {Project} 原有 {Original} 個 PR，過濾後剩餘 {Filtered} 個",
                projectResult.ProjectPath, projectResult.PullRequests.Count, filteredPRs.Count);

            // 建立過濾後的 ProjectResult
            filteredResults.Add(projectResult with { PullRequests = filteredPRs });
        }

        // 4. 建立過濾後的 FetchResult
        var filteredFetchResult = new FetchResult { Results = filteredResults };

        // 5. 寫入目標 資料交換儲存體 Hash
        var targetJson = filteredFetchResult.ToJson();
        await _dataTransferService.SetFieldAsync(TargetDataTransferGroupKey, TargetDataTransferFieldKey, targetJson);

        Logger.LogInformation("過濾完成，結果已寫入 資料交換儲存體 Hash {HashKey} Field {Field}", TargetDataTransferGroupKey, TargetDataTransferFieldKey);

        // 6. 輸出至 stdout
        Console.WriteLine(targetJson);
    }
}
