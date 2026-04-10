namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 代表 Google Sheet 中一個專案區段的位置資訊
/// </summary>
internal class SheetProjectSegment
{
    /// <summary>
    /// 專案名稱（可能包含逗號分隔的多個名稱）
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

    /// <summary>
    /// 判斷此區段是否與指定的專案名稱匹配。
    /// 將 ProjectName 以 ',' 拆開後逐一比對。
    /// </summary>
    public bool MatchesProject(string projectName)
    {
        return ProjectName
            .Split(',')
            .Any(n => n.Trim() == projectName);
    }
}
