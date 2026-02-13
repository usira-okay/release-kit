namespace ReleaseKit.Domain.Common;

/// <summary>
/// 表示操作錯誤，包含錯誤碼與訊息
/// </summary>
/// <param name="Code">錯誤碼（格式：Category.ErrorName，如：SourceControl.ApiError）</param>
/// <param name="Message">人類可讀的錯誤訊息（繁體中文）</param>
/// <remarks>
/// Error 為不可變的錯誤記錄類別，採用 Category.ErrorName 命名規則。
/// 每個錯誤類別提供靜態工廠方法以確保錯誤碼與訊息的一致性。
/// 
/// 錯誤碼格式範例：
/// - SourceControl.BranchNotFound
/// - SourceControl.Unauthorized
/// - SourceControl.RateLimitExceeded
/// 
/// 使用範例：
/// <code>
/// return Result&lt;T&gt;.Failure(Error.SourceControl.Unauthorized);
/// return Result&lt;T&gt;.Failure(Error.SourceControl.BranchNotFound("main"));
/// </code>
/// </remarks>
public sealed record Error(string Code, string Message)
{
    /// <summary>
    /// 表示無錯誤的特殊值
    /// </summary>
    /// <remarks>
    /// 用於表示沒有發生錯誤的情況，Code 與 Message 皆為空字串。
    /// 通常不直接使用，僅作為 Result Pattern 的內部預設值。
    /// </remarks>
    public static readonly Error None = new(string.Empty, string.Empty);

    /// <summary>
    /// 原始碼控制相關錯誤
    /// </summary>
    /// <remarks>
    /// 包含與 GitLab、Bitbucket 等原始碼控制平台互動時可能發生的各類錯誤。
    /// </remarks>
    public static class SourceControl
    {
        /// <summary>
        /// 分支不存在錯誤
        /// </summary>
        /// <param name="branch">分支名稱</param>
        /// <returns>錯誤物件</returns>
        /// <remarks>
        /// 當指定的分支在專案中不存在時使用此錯誤。
        /// </remarks>
        public static Error BranchNotFound(string branch) =>
            new("SourceControl.BranchNotFound", $"分支 '{branch}' 不存在");

        /// <summary>
        /// API 呼叫失敗錯誤
        /// </summary>
        /// <param name="message">詳細錯誤訊息</param>
        /// <returns>錯誤物件</returns>
        /// <remarks>
        /// 通用的 API 錯誤，用於包裝 HTTP 錯誤或其他 API 相關問題。
        /// </remarks>
        public static Error ApiError(string message) =>
            new("SourceControl.ApiError", $"API 呼叫失敗：{message}");

        /// <summary>
        /// API 驗證失敗錯誤
        /// </summary>
        /// <remarks>
        /// 當 Access Token 無效、過期或權限不足時使用此錯誤（HTTP 401 Unauthorized）。
        /// </remarks>
        public static Error Unauthorized =>
            new("SourceControl.Unauthorized", "API 驗證失敗，請檢查 Access Token");

        /// <summary>
        /// API 請求限制已達上限錯誤
        /// </summary>
        /// <remarks>
        /// 當達到 API Rate Limit 時使用此錯誤（HTTP 429 Too Many Requests）。
        /// 建議實作重試機制或延遲後續請求。
        /// </remarks>
        public static Error RateLimitExceeded =>
            new("SourceControl.RateLimitExceeded", "已達到 API 請求限制，請稍後再試");

        /// <summary>
        /// 網路連線錯誤
        /// </summary>
        /// <remarks>
        /// 當無法連線到 API 端點時使用此錯誤（如：網路中斷、DNS 解析失敗、連線逾時）。
        /// </remarks>
        public static Error NetworkError =>
            new("SourceControl.NetworkError", "網路連線錯誤，請檢查網路狀態");

        /// <summary>
        /// API 回應格式無效錯誤
        /// </summary>
        /// <remarks>
        /// 當 API 回應的 JSON 格式無法解析或欄位結構不符合預期時使用此錯誤。
        /// </remarks>
        public static Error InvalidResponse =>
            new("SourceControl.InvalidResponse", "API 回應格式無效");

        /// <summary>
        /// 專案不存在錯誤
        /// </summary>
        /// <param name="projectPath">專案路徑</param>
        /// <returns>錯誤物件</returns>
        /// <remarks>
        /// 當指定的專案路徑在平台上不存在或無權限存取時使用此錯誤（HTTP 404 Not Found）。
        /// </remarks>
        public static Error ProjectNotFound(string projectPath) =>
            new("SourceControl.ProjectNotFound", $"專案 '{projectPath}' 不存在");
    }

    /// <summary>
    /// Azure DevOps 相關錯誤
    /// </summary>
    /// <remarks>
    /// 包含與 Azure DevOps REST API 互動時可能發生的各類錯誤。
    /// </remarks>
    public static class AzureDevOps
    {
        /// <summary>
        /// Work Item 不存在或無權限存取錯誤
        /// </summary>
        /// <param name="workItemId">Work Item 識別碼</param>
        /// <returns>錯誤物件</returns>
        /// <remarks>
        /// 當指定的 Work Item 在 Azure DevOps 中不存在或使用者無權限存取時使用此錯誤（HTTP 404 Not Found）。
        /// </remarks>
        public static Error WorkItemNotFound(int workItemId) =>
            new("AzureDevOps.WorkItemNotFound", $"Work Item '{workItemId}' 不存在或無權限存取");

        /// <summary>
        /// API 呼叫失敗錯誤
        /// </summary>
        /// <param name="message">詳細錯誤訊息</param>
        /// <returns>錯誤物件</returns>
        /// <remarks>
        /// 通用的 Azure DevOps API 錯誤，用於包裝 HTTP 錯誤或其他 API 相關問題。
        /// </remarks>
        public static Error ApiError(string message) =>
            new("AzureDevOps.ApiError", $"Azure DevOps API 呼叫失敗：{message}");

        /// <summary>
        /// API 驗證失敗錯誤
        /// </summary>
        /// <remarks>
        /// 當 Personal Access Token (PAT) 無效、過期或權限不足時使用此錯誤（HTTP 401 Unauthorized）。
        /// </remarks>
        public static Error Unauthorized =>
            new("AzureDevOps.Unauthorized", "Azure DevOps API 驗證失敗，請檢查 Personal Access Token");
    }
}
