using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 相依性邊（描述兩個專案間的關聯）
/// </summary>
public sealed record DependencyEdge
{
    /// <summary>
    /// 來源專案
    /// </summary>
    public required string SourceProject { get; init; }

    /// <summary>
    /// 目標專案
    /// </summary>
    public required string TargetProject { get; init; }

    /// <summary>
    /// 相依類型
    /// </summary>
    public required DependencyType DependencyType { get; init; }

    /// <summary>
    /// 具體目標（API URL、DB 名稱等）
    /// </summary>
    public required string Target { get; init; }
}
