namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 平台層級的 Release 設定輸出
/// </summary>
public record PlatformSettingOutput
{
    /// <summary>
    /// 專案設定清單
    /// </summary>
    public List<ProjectSettingOutput> Projects { get; init; } = new();
}
