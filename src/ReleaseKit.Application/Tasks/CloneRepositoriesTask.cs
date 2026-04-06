using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// Clone 所有設定中的 Repository 任務
/// </summary>
/// <remarks>
/// 從 GitLab 與 Bitbucket 設定中讀取所有專案，
/// 使用 <see cref="IGitService"/> 並行 Clone 至本機，
/// 並將成功的 Clone 路徑對照表寫入 Redis。
/// </remarks>
public class CloneRepositoriesTask : ITask
{
    private readonly IRedisService _redisService;
    private readonly IGitService _gitService;
    private readonly GitLabOptions _gitLabOptions;
    private readonly BitbucketOptions _bitbucketOptions;
    private readonly RiskAnalysisOptions _riskAnalysisOptions;
    private readonly ILogger<CloneRepositoriesTask> _logger;

    /// <summary>
    /// 初始化 <see cref="CloneRepositoriesTask"/> 類別的新執行個體
    /// </summary>
    /// <param name="redisService">Redis 服務</param>
    /// <param name="gitService">Git 操作服務</param>
    /// <param name="gitLabOptions">GitLab 組態</param>
    /// <param name="bitbucketOptions">Bitbucket 組態</param>
    /// <param name="riskAnalysisOptions">風險分析組態</param>
    /// <param name="logger">日誌記錄器</param>
    public CloneRepositoriesTask(
        IRedisService redisService,
        IGitService gitService,
        IOptions<GitLabOptions> gitLabOptions,
        IOptions<BitbucketOptions> bitbucketOptions,
        IOptions<RiskAnalysisOptions> riskAnalysisOptions,
        ILogger<CloneRepositoriesTask> logger)
    {
        _redisService = redisService;
        _gitService = gitService;
        _gitLabOptions = gitLabOptions.Value;
        _bitbucketOptions = bitbucketOptions.Value;
        _riskAnalysisOptions = riskAnalysisOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// 執行 Clone 所有 Repository 任務
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始 Clone 所有 Repository");

        // 清除整個 CloneBasePath，確保乾淨的 Clone 環境
        if (Directory.Exists(_riskAnalysisOptions.CloneBasePath))
        {
            _logger.LogInformation("刪除現有 CloneBasePath：{CloneBasePath}", _riskAnalysisOptions.CloneBasePath);
            Directory.Delete(_riskAnalysisOptions.CloneBasePath, recursive: true);
        }

        var clonePaths = new Dictionary<string, string>();
        var semaphore = new SemaphoreSlim(_riskAnalysisOptions.MaxConcurrentClones);

        var cloneTasks = new List<Task>();

        foreach (var project in _gitLabOptions.Projects ?? [])
        {
            cloneTasks.Add(CloneProjectAsync(
                project.ProjectPath,
                BuildGitLabCloneUrl(project.ProjectPath),
                clonePaths,
                semaphore));
        }

        foreach (var project in _bitbucketOptions.Projects ?? [])
        {
            cloneTasks.Add(CloneProjectAsync(
                project.ProjectPath,
                BuildBitbucketCloneUrl(project.ProjectPath),
                clonePaths,
                semaphore));
        }

        await Task.WhenAll(cloneTasks);

        // 將成功的 Clone 路徑對照表寫入 Redis
        var json = clonePaths.ToJson();
        await _redisService.HashSetAsync(RedisKeys.RiskAnalysisHash, RedisKeys.Fields.ClonePaths, json);

        _logger.LogInformation("Clone 完成，共 {Count} 個 Repository 成功", clonePaths.Count);
    }

    /// <summary>
    /// Clone 單一專案，失敗時記錄警告並繼續
    /// </summary>
    private async Task CloneProjectAsync(
        string projectPath,
        string cloneUrl,
        Dictionary<string, string> clonePaths,
        SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();

        // 統一使用正斜線，避免 Windows 上 Path.Combine 產生反斜線導致 git 路徑問題
        var targetPath = Path.Combine(_riskAnalysisOptions.CloneBasePath, projectPath)
            .Replace('\\', '/');

        _logger.LogInformation("正在 Clone {ProjectPath} 至 {TargetPath}", projectPath, targetPath);

        var result = await _gitService.CloneRepositoryAsync(cloneUrl, targetPath);

        if (result.IsSuccess)
        {
            lock (clonePaths)
            {
                clonePaths[projectPath] = targetPath;
            }
        }
        else
        {
            _logger.LogWarning("Clone 失敗：{ProjectPath}，錯誤：{Error}",
                projectPath, result.Error?.Message);
        }

        semaphore.Release();
    }

    /// <summary>
    /// 建構 GitLab Clone URL（移除 /api/v4 後，使用 oauth2:{PAT} 內嵌認證）
    /// </summary>
    /// <param name="projectPath">專案路徑</param>
    /// <returns>包含 PAT 認證的 GitLab Clone URL</returns>
    internal string BuildGitLabCloneUrl(string projectPath)
    {
        var baseUrl = _gitLabOptions.ApiUrl.Replace("/api/v4", string.Empty);
        var uri = new Uri(baseUrl);
        var encodedToken = Uri.EscapeDataString(_gitLabOptions.AccessToken);
        var basePath = uri.AbsolutePath.TrimEnd('/');
        return $"{uri.Scheme}://oauth2:{encodedToken}@{uri.Host}{basePath}/{projectPath}.git";
    }

    /// <summary>
    /// 建構 Bitbucket Clone URL（使用 x-token-auth 內嵌認證）。
    /// Bitbucket 支援以 x-token-auth:{access_token} 進行 git 認證，
    /// 無需提供 username 或 email。
    /// </summary>
    /// <param name="projectPath">專案路徑</param>
    /// <returns>Bitbucket Clone URL</returns>
    internal string BuildBitbucketCloneUrl(string projectPath)
    {
        var encodedToken = Uri.EscapeDataString(_bitbucketOptions.AccessToken);
        return $"https://x-token-auth:{encodedToken}@bitbucket.org/{projectPath}.git";
    }
}
