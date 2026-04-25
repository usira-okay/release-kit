namespace ReleaseKit.Application.Tasks;

/// <summary>
/// Release 設定輸出（包含 GitLab 與 Bitbucket 兩個平台）
/// </summary>
public record ReleaseSettingOutput
{
    /// <summary>
    /// GitLab 平台設定
    /// </summary>
    public PlatformSettingOutput GitLab { get; init; } = new();

    /// <summary>
    /// Bitbucket 平台設定
    /// </summary>
    public PlatformSettingOutput Bitbucket { get; init; } = new();
}
