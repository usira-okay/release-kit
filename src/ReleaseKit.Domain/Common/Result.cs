namespace ReleaseKit.Domain.Common;

/// <summary>
/// 表示操作結果，包含成功值或錯誤資訊
/// </summary>
/// <typeparam name="T">成功時的回傳值類型</typeparam>
public class Result<T>
{
    /// <summary>
    /// 成功時的回傳值
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// 失敗時的錯誤資訊
    /// </summary>
    public Error? Error { get; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess => Error is null;

    /// <summary>
    /// 是否失敗
    /// </summary>
    public bool IsFailure => !IsSuccess;

    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    /// <summary>
    /// 建立成功結果
    /// </summary>
    /// <param name="value">成功值</param>
    /// <returns>成功的 Result 物件</returns>
    public static Result<T> Success(T value) => new(value, null);

    /// <summary>
    /// 建立失敗結果
    /// </summary>
    /// <param name="error">錯誤資訊</param>
    /// <returns>失敗的 Result 物件</returns>
    public static Result<T> Failure(Error error) => new(default, error);
}
