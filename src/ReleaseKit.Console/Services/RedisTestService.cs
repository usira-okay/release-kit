using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Console.Services;

/// <summary>
/// Redis 測試服務，用於驗證 Redis 讀寫功能
/// </summary>
public class RedisTestService
{
    private readonly IRedisService _redisService;
    private readonly ILogger<RedisTestService> _logger;

    public RedisTestService(
        IRedisService redisService,
        ILogger<RedisTestService> logger)
    {
        _redisService = redisService ?? throw new ArgumentNullException(nameof(redisService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 執行 Redis 讀寫測試
    /// </summary>
    public async Task RunTestAsync()
    {
        _logger.LogInformation("=== 開始 Redis 測試 ===");

        try
        {
            // 測試 1: 寫入與讀取
            var testKey = "test:hello";
            var testValue = $"Hello from ReleaseKit! Time: {DateTime.UtcNow:O}";
            
            _logger.LogInformation("測試 1: 寫入資料");
            var setResult = await _redisService.SetAsync(testKey, testValue);
            _logger.LogInformation("寫入結果: {Result}, Key: {Key}, Value: {Value}", 
                setResult ? "成功" : "失敗", testKey, testValue);

            _logger.LogInformation("測試 2: 讀取資料");
            var getValue = await _redisService.GetAsync(testKey);
            _logger.LogInformation("讀取結果: {Value}", getValue ?? "(null)");

            if (getValue == testValue)
            {
                _logger.LogInformation("✅ 讀取值與寫入值相符");
            }
            else
            {
                _logger.LogWarning("❌ 讀取值與寫入值不符");
            }

            // 測試 3: 檢查鍵值是否存在
            _logger.LogInformation("測試 3: 檢查鍵值是否存在");
            var exists = await _redisService.ExistsAsync(testKey);
            _logger.LogInformation("鍵值存在: {Exists}", exists);

            // 測試 4: 帶過期時間的寫入
            var expiryKey = "test:expiry";
            var expiryValue = "This will expire in 60 seconds";
            _logger.LogInformation("測試 4: 寫入帶過期時間的資料 (60秒)");
            var expiryResult = await _redisService.SetAsync(expiryKey, expiryValue, TimeSpan.FromSeconds(60));
            _logger.LogInformation("寫入結果: {Result}", expiryResult ? "成功" : "失敗");

            // 測試 5: 刪除鍵值
            _logger.LogInformation("測試 5: 刪除鍵值");
            var deleteResult = await _redisService.DeleteAsync(testKey);
            _logger.LogInformation("刪除結果: {Result}", deleteResult ? "成功" : "失敗");

            // 驗證是否已刪除
            var existsAfterDelete = await _redisService.ExistsAsync(testKey);
            _logger.LogInformation("刪除後檢查: {Exists}", existsAfterDelete ? "仍存在" : "已刪除");

            _logger.LogInformation("=== Redis 測試完成 ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis 測試執行失敗");
        }
    }
}
