namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// 任務介面，定義所有任務必須實作的執行方法
/// </summary>
public interface ITask
{
    /// <summary>
    /// 執行任務
    /// </summary>
    /// <returns>執行結果</returns>
    Task ExecuteAsync();
}
