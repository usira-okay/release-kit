# Research: 取得各 Repository 最新 Release Branch 名稱

**Feature Branch**: `002-fetch-release-branch`
**Date**: 2026-02-10

## Research Tasks

### R1: 現有 GetBranchesAsync 實作與行為

**Decision**: 直接複用現有 `ISourceControlRepository.GetBranchesAsync(projectPath, pattern)` 方法，傳入 `"release/"` 作為前綴篩選。

**Rationale**:
- GitLab 與 Bitbucket Repository 皆已實作此方法
- 方法支援前綴篩選（`StartsWith`），傳入 `"release/"` 即可取得所有 release branch
- 回傳 `Result<IReadOnlyList<string>>`，已使用 Result Pattern
- 回傳的分支名稱為完整名稱（如 `release/20260101`），可直接使用字串排序

**Alternatives considered**:
- 新增專用的 `GetReleaseBranchesAsync` 方法 → 不必要，現有方法已滿足需求
- 使用 API 分頁取得所有分支再過濾 → 現有實作已處理分頁

### R2: 輸出格式設計

**Decision**: 使用 `Dictionary<string, List<string>>` 作為輸出結構，key 為 release branch 名稱（或 `"NotFound"`），value 為專案路徑清單。

**Rationale**:
- 與使用者提供的範例格式完全一致
- 透過 `JsonExtensions.ToJson()` 序列化後，自然產生 `{ "release/20260101": ["path1", ...] }` 格式
- `Dictionary` 的 key 具有唯一性，天然支援分組邏輯

**Alternatives considered**:
- 類似 `FetchResult` / `ProjectResult` 的 record 結構 → 不符合使用者要求的輸出格式
- 自訂 JSON Converter → 過度設計，`Dictionary` 已可直接序列化為目標格式

### R3: 基底任務類別設計

**Decision**: 新增 `BaseFetchReleaseBranchTask<TOptions, TProjectOptions>`，不繼承現有 `BaseFetchPullRequestsTask`。

**Rationale**:
- 現有 `BaseFetchPullRequestsTask` 含有大量 PR 相關邏輯（FetchMode、DateTimeRange、BranchDiff），與 release branch 查詢無關
- 新增獨立基底類別更符合 SRP（單一職責原則）
- 仍遵循相同的模板方法模式：子類別提供 `PlatformName`、`Platform`、`RedisKey`、`GetProjects()`

**Alternatives considered**:
- 繼承 `BaseFetchPullRequestsTask` 並覆寫 `ExecuteAsync` → 違反 LSP，且需引入不必要的泛型參數
- 直接實作 `ITask` 不使用基底類別 → 導致 GitLab 與 Bitbucket 任務重複程式碼

### R4: Redis Key 命名

**Decision**: 使用 `"GitLab:ReleaseBranches"` 與 `"Bitbucket:ReleaseBranches"` 作為 Redis Key。

**Rationale**:
- 遵循現有 `RedisKeys` 命名慣例（`平台名稱:資料類型`）
- 與現有 `GitLab:PullRequests` / `Bitbucket:PullRequests` 命名格式一致
- 完整 Redis Key（含 InstanceName prefix）為 `ReleaseKit:GitLab:ReleaseBranches`

**Alternatives considered**:
- `GitLab:LatestReleaseBranch` → 資料內容是所有專案的 release branch 分組，非單一 branch
- `GitLab:Branches` → 太模糊，未來可能有其他分支查詢功能

### R5: 最新 Release Branch 判定邏輯

**Decision**: 使用字串排序（`OrderByDescending`）取得最後一個作為「最新」分支。

**Rationale**:
- Release branch 命名慣例為 `release/YYYYMMDD`，字母排序等同時間排序
- 簡單直觀，無需額外的日期解析邏輯
- `GetBranchesAsync` 回傳的清單可能已排序，但不依賴此假設

**Alternatives considered**:
- 解析 `YYYYMMDD` 為日期再比較 → 過度設計，若格式不為日期則失敗
- 取建立時間最新的分支 → 需額外 API 呼叫取得分支 metadata

### R6: 錯誤處理策略

**Decision**: `GetBranchesAsync` 回傳 `IsFailure` 或空清單時，將專案歸類到 `"NotFound"` 群組，不中斷整體流程。

**Rationale**:
- 符合 spec 中的 Edge Case 要求
- 與現有 `BaseFetchPullRequestsTask` 中的 try-catch per project 邏輯一致
- 使用者可一次看到所有失敗的專案

**Alternatives considered**:
- 拋出例外中斷整體流程 → 不符合需求，單一失敗不應影響其他專案
- 使用 Result Pattern 包裝整體結果 → 過度設計，整體流程不需回傳 Result
