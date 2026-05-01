using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis.Models;

/// <summary>
/// Synthesis Agent 的輸入資料
/// </summary>
public sealed record SynthesisInput
{
    /// <summary>
    /// 專案路徑
    /// </summary>
    public required string ProjectPath { get; init; }

    /// <summary>
    /// 所有 Expert 的分析結果（依情境分組）
    /// </summary>
    public required IReadOnlyDictionary<RiskScenario, ExpertFindings> ExpertResults { get; init; }

    /// <summary>
    /// 其他專案的摘要資訊（供跨專案推斷，軟性依賴 Stage 3）
    /// </summary>
    public IReadOnlyList<string>? OtherProjectsSummary { get; init; }
}
