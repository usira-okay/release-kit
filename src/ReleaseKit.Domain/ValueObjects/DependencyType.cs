namespace ReleaseKit.Domain.ValueObjects;

/// <summary>
/// 推斷的服務相依類型
/// </summary>
public enum DependencyType
{
    /// <summary>
    /// 共用 NuGet 套件
    /// </summary>
    NuGet,

    /// <summary>
    /// HTTP API 呼叫
    /// </summary>
    HttpCall,

    /// <summary>
    /// 共用資料庫
    /// </summary>
    SharedDb,

    /// <summary>
    /// 共用訊息佇列
    /// </summary>
    SharedMQ
}
