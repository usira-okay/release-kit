namespace ReleaseKit.Console.Options;

/// <summary>
/// 使用者對應設定
/// </summary>
public class UserMappingOptions
{
    /// <summary>
    /// 使用者對應清單
    /// </summary>
    public List<UserMappingItem> Mappings { get; set; } = new();
}
