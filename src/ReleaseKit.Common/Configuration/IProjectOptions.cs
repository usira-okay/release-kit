namespace ReleaseKit.Common.Configuration;

/// <summary>
/// 專案配置選項介面
/// </summary>
public interface IProjectOptions
{
    /// <summary>
    /// 專案路徑
    /// </summary>
    string ProjectPath { get; }

    /// <summary>
    /// 目標分支名稱
    /// </summary>
    string TargetBranch { get; }

    /// <summary>
    /// 拉取模式：DateTimeRange 或 BranchDiff（可選，若未提供則使用全域設定）
    /// </summary>
    FetchMode? FetchMode { get; }

    /// <summary>
    /// 來源分支名稱（BranchDiff 模式時必填）
    /// </summary>
    string? SourceBranch { get; }

    /// <summary>
    /// 開始時間（DateTimeRange 模式時必填）
    /// </summary>
    DateTimeOffset? StartDateTime { get; }

    /// <summary>
    /// 結束時間（DateTimeRange 模式時必填）
    /// </summary>
    DateTimeOffset? EndDateTime { get; }
}
