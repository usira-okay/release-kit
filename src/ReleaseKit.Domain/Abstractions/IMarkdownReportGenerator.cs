using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// Markdown 報告產生器介面
/// </summary>
public interface IMarkdownReportGenerator
{
    /// <summary>
    /// 從 RiskReport 產生 Markdown 報告
    /// </summary>
    /// <param name="report">風險報告資料模型</param>
    /// <returns>Markdown 格式的報告字串</returns>
    string Generate(RiskReport report);
}
