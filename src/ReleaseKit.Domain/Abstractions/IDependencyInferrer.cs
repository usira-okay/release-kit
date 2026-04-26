using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// 跨專案相依性推斷介面
/// </summary>
public interface IDependencyInferrer
{
    /// <summary>
    /// 根據多個專案結構推斷相依性
    /// </summary>
    /// <param name="projectStructures">所有專案的結構分析結果</param>
    /// <returns>含相依性的更新後專案結構清單</returns>
    IReadOnlyList<ProjectStructure> InferDependencies(IReadOnlyList<ProjectStructure> projectStructures);
}
