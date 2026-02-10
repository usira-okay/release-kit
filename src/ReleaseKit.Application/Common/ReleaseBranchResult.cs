namespace ReleaseKit.Application.Common;

/// <summary>
/// Release Branch 查詢結果
/// </summary>
/// <param name="Branches">Release Branch 名稱與專案路徑對應字典</param>
public sealed record ReleaseBranchResult(Dictionary<string, List<string>> Branches);
