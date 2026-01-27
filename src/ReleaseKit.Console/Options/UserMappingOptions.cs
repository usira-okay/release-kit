namespace ReleaseKit.Console.Options;

/// <summary>
/// 使用者對應設定
/// </summary>
public class UserMappingOptions
{
    private List<UserMappingItem> _mappings = new();

    /// <summary>
    /// 使用者對應清單
    /// </summary>
    public List<UserMappingItem> Mappings
    {
        get => _mappings;
        set => _mappings = value ?? new();
    }
}
