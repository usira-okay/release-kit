namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 代表 Google Sheet 中一個專案區段的位置資訊
/// </summary>
internal class SheetProjectSegment
{
    /// <summary>
    /// 專案名稱
    /// </summary>
    public string ProjectName { get; init; } = string.Empty;

    /// <summary>
    /// 專案表頭列的 0-based row index
    /// </summary>
    public int HeaderRowIndex { get; init; }

    /// <summary>
    /// 資料起始列的 0-based row index
    /// </summary>
    public int DataStartRowIndex { get; init; }

    /// <summary>
    /// 資料結束列的 0-based row index
    /// </summary>
    public int DataEndRowIndex { get; init; }
}
