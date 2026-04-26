using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 推斷的服務相依性
/// </summary>
public sealed record ServiceDependency
{
    /// <summary>
    /// 相依類型
    /// </summary>
    public required DependencyType DependencyType { get; init; }

    /// <summary>
    /// 目標：套件名稱、API URL、DB 名稱、MQ Topic
    /// </summary>
    public required string Target { get; init; }
}
