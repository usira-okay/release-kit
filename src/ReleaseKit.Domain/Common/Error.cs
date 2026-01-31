namespace ReleaseKit.Domain.Common;

/// <summary>
/// 表示操作錯誤，包含錯誤碼與訊息
/// </summary>
/// <param name="Code">錯誤碼（格式：Category.ErrorName）</param>
/// <param name="Message">人類可讀的錯誤訊息</param>
public sealed record Error(string Code, string Message)
{
    /// <summary>
    /// 表示無錯誤
    /// </summary>
    public static readonly Error None = new(string.Empty, string.Empty);

    /// <summary>
    /// 原始碼控制相關錯誤
    /// </summary>
    public static class SourceControl
    {
        /// <summary>
        /// 分支不存在錯誤
        /// </summary>
        /// <param name="branch">分支名稱</param>
        /// <returns>錯誤物件</returns>
        public static Error BranchNotFound(string branch) =>
            new("SourceControl.BranchNotFound", $"分支 '{branch}' 不存在");

        /// <summary>
        /// API 呼叫失敗錯誤
        /// </summary>
        /// <param name="message">錯誤訊息</param>
        /// <returns>錯誤物件</returns>
        public static Error ApiError(string message) =>
            new("SourceControl.ApiError", $"API 呼叫失敗：{message}");

        /// <summary>
        /// API 驗證失敗錯誤
        /// </summary>
        public static Error Unauthorized =>
            new("SourceControl.Unauthorized", "API 驗證失敗，請檢查 Access Token");

        /// <summary>
        /// API 請求限制已達上限錯誤
        /// </summary>
        public static Error RateLimitExceeded =>
            new("SourceControl.RateLimitExceeded", "已達到 API 請求限制，請稍後再試");

        /// <summary>
        /// 網路連線錯誤
        /// </summary>
        public static Error NetworkError =>
            new("SourceControl.NetworkError", "網路連線錯誤，請檢查網路狀態");

        /// <summary>
        /// API 回應格式無效錯誤
        /// </summary>
        public static Error InvalidResponse =>
            new("SourceControl.InvalidResponse", "API 回應格式無效");

        /// <summary>
        /// 專案不存在錯誤
        /// </summary>
        /// <param name="projectPath">專案路徑</param>
        /// <returns>錯誤物件</returns>
        public static Error ProjectNotFound(string projectPath) =>
            new("SourceControl.ProjectNotFound", $"專案 '{projectPath}' 不存在");
    }
}
