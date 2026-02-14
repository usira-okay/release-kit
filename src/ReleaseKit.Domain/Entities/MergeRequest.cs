using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 表示一個已合併的 Pull Request / Merge Request
/// </summary>
/// <remarks>
/// 統一表示 GitLab Merge Request 與 Bitbucket Pull Request 的實體模型。
/// 所有屬性均為必填（required），確保資料完整性。
/// 時間欄位統一使用 DateTimeOffset（UTC 時區）。
/// 此實體為不可變（immutable）record 類型，遵循 DDD 實體原則。
/// </remarks>
public sealed record MergeRequest
{
    /// <summary>
    /// PR/MR 識別碼
    /// </summary>
    /// <remarks>
    /// GitLab: 對應 iid 欄位（專案內唯一編號）
    /// Bitbucket: 對應 id 欄位（Repository 內唯一編號）
    /// </remarks>
    public required int PullRequestId { get; init; }

    /// <summary>
    /// PR/MR 標題
    /// </summary>
    /// <remarks>
    /// 對應 GitLab 的 title 欄位或 Bitbucket 的 title 欄位。
    /// 不可為 null 或空白字串。
    /// </remarks>
    public required string Title { get; init; }

    /// <summary>
    /// PR/MR 描述
    /// </summary>
    /// <remarks>
    /// 對應 GitLab 的 description 欄位或 Bitbucket 的 summary.raw 欄位。
    /// 可為 null（當 PR/MR 沒有描述時）。
    /// </remarks>
    public string? Description { get; init; }

    /// <summary>
    /// 來源分支名稱
    /// </summary>
    /// <remarks>
    /// 對應 GitLab 的 source_branch 欄位或 Bitbucket 的 source.branch.name 欄位。
    /// 表示 PR/MR 的變更來自哪個分支。
    /// </remarks>
    public required string SourceBranch { get; init; }

    /// <summary>
    /// 目標分支名稱
    /// </summary>
    /// <remarks>
    /// 對應 GitLab 的 target_branch 欄位或 Bitbucket 的 destination.branch.name 欄位。
    /// 表示 PR/MR 合併到哪個分支。
    /// </remarks>
    public required string TargetBranch { get; init; }

    /// <summary>
    /// 建立時間 (UTC)
    /// </summary>
    /// <remarks>
    /// 對應 GitLab 的 created_at 欄位或 Bitbucket 的 created_on 欄位。
    /// 統一使用 UTC 時區的 DateTimeOffset。
    /// </remarks>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// 合併時間 (UTC)
    /// </summary>
    /// <remarks>
    /// 對應 GitLab 的 merged_at 欄位或 Bitbucket 的 closed_on 欄位。
    /// 統一使用 UTC 時區的 DateTimeOffset。
    /// 此欄位為判斷 PR/MR 是否在時間區間內的關鍵欄位。
    /// 若 PR/MR 尚未合併，此值為 null。
    /// </remarks>
    public required DateTimeOffset? MergedAt { get; init; }

    /// <summary>
    /// 狀態（通常為 "merged"）
    /// </summary>
    /// <remarks>
    /// 對應 GitLab 的 state 欄位或 Bitbucket 的 state 欄位。
    /// 在此功能中，僅處理已合併的 PR/MR，因此通常為 "merged"。
    /// </remarks>
    public required string State { get; init; }

    /// <summary>
    /// 作者 ID
    /// </summary>
    /// <remarks>
    /// 對應 GitLab 的 author.id（轉為字串）或 Bitbucket 的 author.uuid 欄位。
    /// 用於唯一識別 PR/MR 的建立者。
    /// </remarks>
    public required string AuthorUserId { get; init; }

    /// <summary>
    /// 作者名稱
    /// </summary>
    /// <remarks>
    /// 對應 GitLab 的 author.username 欄位或 Bitbucket 的 author.display_name 欄位。
    /// 用於顯示 PR/MR 作者的可讀名稱。
    /// </remarks>
    public required string AuthorName { get; init; }

    /// <summary>
    /// PR/MR 網址
    /// </summary>
    /// <remarks>
    /// 對應 GitLab 的 web_url 欄位或 Bitbucket 的 links.html.href 欄位。
    /// 提供可直接存取 PR/MR 詳細頁面的完整 URL。
    /// </remarks>
    public required string PRUrl { get; init; }

    /// <summary>
    /// 來源平台
    /// </summary>
    /// <remarks>
    /// 表示此 PR/MR 來自 GitLab 或 Bitbucket 平台。
    /// 使用 SourceControlPlatform 列舉值區分不同平台。
    /// </remarks>
    public required SourceControlPlatform Platform { get; init; }

    /// <summary>
    /// 專案路徑（如：mygroup/backend-api）
    /// </summary>
    /// <remarks>
    /// GitLab 格式：group/project（如：mycompany/backend-api）
    /// Bitbucket 格式：workspace/repo_slug（如：myworkspace/backend-api）
    /// 用於識別 PR/MR 所屬的專案。
    /// </remarks>
    public required string ProjectPath { get; init; }
}
