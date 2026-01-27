namespace ReleaseKit.Console.Options;

/// <summary>
/// Bitbucket 專案設定
/// </summary>
public class BitbucketProjectOptions
{
    /// <summary>
    /// 專案路徑（例如：mygroup/backend-api）
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// 目標分支（例如：main）
    /// </summary>
    public string TargetBranch { get; set; } = string.Empty;
}
