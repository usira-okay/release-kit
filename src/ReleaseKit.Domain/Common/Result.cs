namespace ReleaseKit.Domain.Common;

/// <summary>
/// 表示操作結果，包含成功值或錯誤資訊
/// </summary>
/// <typeparam name="T">成功時的回傳值類型</typeparam>
/// <remarks>
/// Result Pattern 用於取代傳統的例外處理機制，提供更明確的錯誤處理流程。
/// 使用此模式可避免 try-catch 造成的效能損耗，並強制呼叫端處理錯誤情況。
/// 
/// 使用範例：
/// <code>
/// var result = await repository.GetMergeRequestsByDateRangeAsync(...);
/// if (result.IsSuccess)
/// {
///     var mergeRequests = result.Value;
///     // 處理成功情況
/// }
/// else
/// {
///     var error = result.Error;
///     // 處理失敗情況
/// }
/// </code>
/// </remarks>
public class Result<T>
{
    /// <summary>
    /// 成功時的回傳值
    /// </summary>
    /// <remarks>
    /// 當 IsSuccess 為 true 時，此屬性包含實際的回傳值。
    /// 當 IsFailure 為 true 時，此屬性為 null 或 default(T)。
    /// </remarks>
    public T? Value { get; }

    /// <summary>
    /// 失敗時的錯誤資訊
    /// </summary>
    /// <remarks>
    /// 當 IsFailure 為 true 時，此屬性包含錯誤詳細資訊。
    /// 當 IsSuccess 為 true 時，此屬性為 null。
    /// </remarks>
    public Error? Error { get; }

    /// <summary>
    /// 是否成功
    /// </summary>
    /// <remarks>
    /// 當 Error 為 null 時回傳 true，表示操作成功完成。
    /// </remarks>
    public bool IsSuccess => Error is null;

    /// <summary>
    /// 是否失敗
    /// </summary>
    /// <remarks>
    /// 當 Error 不為 null 時回傳 true，表示操作執行失敗。
    /// </remarks>
    public bool IsFailure => !IsSuccess;

    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    /// <summary>
    /// 建立成功結果
    /// </summary>
    /// <param name="value">成功時的回傳值</param>
    /// <returns>包含成功值的 Result 物件</returns>
    /// <remarks>
    /// 使用此方法建立表示操作成功的 Result 物件。
    /// Error 屬性會自動設為 null，IsSuccess 會回傳 true。
    /// </remarks>
    public static Result<T> Success(T value) => new(value, null);

    /// <summary>
    /// 建立失敗結果
    /// </summary>
    /// <param name="error">錯誤資訊物件</param>
    /// <returns>包含錯誤資訊的 Result 物件</returns>
    /// <remarks>
    /// 使用此方法建立表示操作失敗的 Result 物件。
    /// Value 屬性會自動設為 default(T)，IsFailure 會回傳 true。
    /// </remarks>
    public static Result<T> Failure(Error error) => new(default, error);
}
