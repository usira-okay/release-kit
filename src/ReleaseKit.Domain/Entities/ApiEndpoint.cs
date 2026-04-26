namespace ReleaseKit.Domain.Entities;

/// <summary>
/// API 端點資訊
/// </summary>
public sealed record ApiEndpoint
{
    /// <summary>
    /// HTTP 方法（GET、POST、PUT、DELETE 等）
    /// </summary>
    public required string HttpMethod { get; init; }

    /// <summary>
    /// 路由路徑
    /// </summary>
    public required string Route { get; init; }

    /// <summary>
    /// Controller 名稱
    /// </summary>
    public required string ControllerName { get; init; }

    /// <summary>
    /// Action 方法名稱
    /// </summary>
    public required string ActionName { get; init; }
}
