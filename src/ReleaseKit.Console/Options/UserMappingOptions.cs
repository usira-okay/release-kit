namespace ReleaseKit.Console.Options;

/// <summary>
/// 使用者對應配置選項
/// </summary>
public class UserMappingOptions
{
    /// <summary>
    /// 組態區段名稱
    /// </summary>
    public const string SectionName = "UserMapping";

    /// <summary>
    /// 使用者對應清單
    /// </summary>
    public List<UserMappingConfig> Mappings { get; set; } = new();
}
