using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Infrastructure.Time;

/// <summary>
/// 系統時間服務實作
/// </summary>
public class SystemNow : INow
{
    /// <summary>
    /// 取得當前 UTC 時間
    /// </summary>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
