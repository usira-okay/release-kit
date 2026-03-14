namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// 標題選擇服務介面
/// </summary>
/// <remarks>
/// 接收多組候選標題，批次從中選出最適合的標題。
/// 實作可串接 AI 服務（如 GitHub Copilot SDK）進行語意分析。
/// </remarks>
public interface ITitleEnhancer
{
    /// <summary>
    /// 批次從候選標題中選出最適合的標題
    /// </summary>
    /// <param name="titleGroups">
    /// 每個項目的候選標題清單（依優先順序排列），
    /// 外層 list 代表不同項目，內層 list 代表該項目的候選標題
    /// </param>
    /// <returns>選出的標題清單，順序與輸入一致，每個標題必須來自對應的候選清單</returns>
    Task<IReadOnlyList<string>> EnhanceTitlesAsync(IReadOnlyList<IReadOnlyList<string>> titleGroups);
}
