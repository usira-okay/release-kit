using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// 專案結構掃描器介面
/// </summary>
public interface IProjectStructureScanner
{
    /// <summary>
    /// 掃描指定路徑的專案結構
    /// </summary>
    /// <param name="projectPath">專案識別路徑（如 mygroup/backend-api）</param>
    /// <param name="localPath">本地專案目錄路徑</param>
    /// <returns>專案結構分析結果</returns>
    ProjectStructure Scan(string projectPath, string localPath);
}
