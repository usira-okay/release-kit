using ReleaseKit.Application.Tasks;

namespace ReleaseKit.Console.Parsers;

/// <summary>
/// 命令列解析結果
/// </summary>
public class ParseResult
{
    /// <summary>
    /// 是否解析成功
    /// </summary>
    public bool IsSuccess { get; private init; }
    
    /// <summary>
    /// 任務類型（成功時）
    /// </summary>
    public TaskType? TaskType { get; private init; }
    
    /// <summary>
    /// 錯誤訊息（失敗時）
    /// </summary>
    public string? ErrorMessage { get; private init; }

    private ParseResult() { }

    /// <summary>
    /// 建立成功的解析結果
    /// </summary>
    public static ParseResult Success(TaskType taskType) => new()
    {
        IsSuccess = true,
        TaskType = taskType
    };

    /// <summary>
    /// 建立失敗的解析結果
    /// </summary>
    public static ParseResult Failure(string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage
    };
}
