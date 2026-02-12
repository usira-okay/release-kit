namespace ReleaseKit.Common.Configuration;

/// <summary>
/// 使用者對應設定選項
/// </summary>
public class UserMappingOptions
{
    /// <summary>
    /// 使用者對應清單
    /// </summary>
    public List<UserMapping> Mappings { get; set; } = new();
}
