namespace ReleaseKit.Application.Common.RiskAnalysis;

/// <summary>
/// 服務相依性資訊（Phase 4 跨服務分析使用）
/// </summary>
public sealed record ServiceDependencyInfo
{
    /// <summary>服務名稱</summary>
    public required string ServiceName { get; init; }

    /// <summary>該服務公開的 API endpoints</summary>
    public required IReadOnlyList<string> ExposedEndpoints { get; init; }

    /// <summary>該服務呼叫的其他服務 endpoints</summary>
    public required IReadOnlyList<string> ConsumedEndpoints { get; init; }

    /// <summary>該服務使用的共用套件及版本</summary>
    public required IReadOnlyList<string> SharedPackages { get; init; }
}
