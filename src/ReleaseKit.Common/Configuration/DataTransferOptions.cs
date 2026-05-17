namespace ReleaseKit.Common.Configuration;

/// <summary>
/// 指令間資料交換設定
/// </summary>
public class DataTransferOptions
{
    /// <summary>
    /// 資料交換提供者（Redis 或 FileSystem）
    /// </summary>
    public DataTransferProvider Provider { get; init; }

    /// <summary>
    /// FileSystem 提供者使用的資料目錄
    /// </summary>
    public string? FileDirectory { get; init; }
}
