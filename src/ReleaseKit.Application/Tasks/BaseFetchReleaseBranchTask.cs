using Microsoft.Extensions.Logging;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 拉取 Release Branch 資訊任務抽象基礎類別
/// </summary>
/// <typeparam name="TOptions">平台配置選項類型</typeparam>
/// <typeparam name="TProjectOptions">專案配置選項類型</typeparam>
public abstract class BaseFetchReleaseBranchTask<TOptions, TProjectOptions> : ITask
    where TProjectOptions : IProjectOptions
{
    private readonly ISourceControlRepository _repository;
    private readonly ILogger _logger;
    private readonly IRedisService _redisService;

    /// <summary>
    /// 平台配置選項
    /// </summary>
    protected TOptions PlatformOptions { get; }

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="repository">原始碼控制倉儲</param>
    /// <param name="logger">日誌記錄器</param>
    /// <param name="redisService">Redis 快取服務</param>
    /// <param name="platformOptions">平台配置選項</param>
    protected BaseFetchReleaseBranchTask(
        ISourceControlRepository repository,
        ILogger logger,
        IRedisService redisService,
        TOptions platformOptions)
    {
        _repository = repository;
        _logger = logger;
        _redisService = redisService;
        PlatformOptions = platformOptions;
    }

    /// <summary>
    /// 取得平台名稱
    /// </summary>
    protected abstract string PlatformName { get; }

    /// <summary>
    /// 取得 Redis 儲存鍵值
    /// </summary>
    protected abstract string RedisKey { get; }

    /// <summary>
    /// 取得專案清單
    /// </summary>
    protected abstract IEnumerable<TProjectOptions> GetProjects();

    /// <summary>
    /// 執行拉取 Release Branch 資訊任務
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始執行 {Platform} Release Branch 拉取任務", PlatformName);

        // 檢查並清除 Redis 中的舊資料
        if (await _redisService.ExistsAsync(RedisKey))
        {
            _logger.LogInformation("清除 Redis 中的舊資料，Key: {RedisKey}", RedisKey);
            await _redisService.DeleteAsync(RedisKey);
        }

        // 儲存結果：key = release branch 名稱，value = 專案路徑清單
        var branchGroups = new Dictionary<string, List<string>>();
        var notFoundProjects = new List<string>();

        var successCount = 0;
        var failureCount = 0;

        // 處理每個專案
        foreach (var project in GetProjects())
        {
            _logger.LogInformation("處理專案: {ProjectPath}", project.ProjectPath);

            // 呼叫 GetBranchesAsync 查詢 release/ 開頭的分支
            var result = await _repository.GetBranchesAsync(project.ProjectPath, "release/");

            if (result.IsSuccess && result.Value != null && result.Value.Count > 0)
            {
                // 成功且有分支：取最新分支（字母排序最大的）
                var latestBranch = result.Value.OrderByDescending(b => b).First();
                _logger.LogInformation("專案 {ProjectPath} 最新 Release Branch: {Branch}", project.ProjectPath, latestBranch);

                // 加入對應分支名稱的分組
                if (!branchGroups.ContainsKey(latestBranch))
                {
                    branchGroups[latestBranch] = new List<string>();
                }
                branchGroups[latestBranch].Add(project.ProjectPath);

                successCount++;
            }
            else
            {
                // 失敗或無分支：加入 NotFound 清單
                _logger.LogWarning("專案 {ProjectPath} 無 Release Branch 或查詢失敗", project.ProjectPath);
                notFoundProjects.Add(project.ProjectPath);
                failureCount++;
            }
        }

        // 將 NotFound 專案加入結果（如果有的話）
        if (notFoundProjects.Count > 0)
        {
            branchGroups["NotFound"] = notFoundProjects;
        }

        // 序列化並輸出到 Console
        var json = branchGroups.ToJson();
        Console.WriteLine(json);

        // 存入 Redis
        await _redisService.SetAsync(RedisKey, json);

        _logger.LogInformation(
            "完成 {Platform} Release Branch 拉取任務，總專案數: {Total}，成功: {Success}，失敗/無分支: {Failure}",
            PlatformName,
            successCount + failureCount,
            successCount,
            failureCount);
    }
}
