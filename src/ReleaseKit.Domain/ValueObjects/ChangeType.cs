namespace ReleaseKit.Domain.ValueObjects;

/// <summary>
/// 檔案變更類型列舉
/// </summary>
public enum ChangeType
{
    /// <summary>
    /// 新增檔案
    /// </summary>
    Added,

    /// <summary>
    /// 修改檔案
    /// </summary>
    Modified,

    /// <summary>
    /// 刪除檔案
    /// </summary>
    Deleted
}
