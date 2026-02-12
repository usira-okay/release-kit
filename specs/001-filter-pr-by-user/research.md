# Research: Filter Pull Requests by User

**Feature**: 001-filter-pr-by-user
**Date**: 2026-02-12

## 研究項目

### 1. 過濾任務基底類別設計

**Decision**: 建立 `BaseFilterPullRequestsByUserTask` 抽象類別，封裝共用的讀取-過濾-寫入邏輯。

**Rationale**:
- 現有 `BaseFetchPullRequestsTask<TOptions, TProjectOptions>` 已建立泛型基底類別模式
- 過濾任務較簡單，不需要平台配置泛型參數，僅需區分讀取來源 Redis Key、寫入目標 Redis Key、以及取得對應平台 UserId 的邏輯
- 基底類別依賴 `IRedisService` 和 `UserMappingOptions`，子類別僅需提供平台特定資訊

**Alternatives considered**:
- 不使用基底類別，兩個 Task 各自獨立實作 → 違反 DRY，過濾邏輯重複
- 使用 Strategy Pattern 而非繼承 → 過度設計，目前僅兩個平台且邏輯簡單

### 2. UserMappingOptions 層級位置

**Decision**: `UserMappingOptions` 目前定義在 `ReleaseKit.Console.Options`，基底類別位於 `ReleaseKit.Application.Tasks`。需透過建構子注入 `IOptions<UserMappingOptions>` 來解決跨層依賴。

**Rationale**:
- `UserMappingOptions` 是 Options pattern 的 POCO 類別，不含業務邏輯
- Application 層的 Task 依賴 Console 層的 Options 類別會造成反向依賴
- 解決方案：將 `UserMappingOptions` 和 `UserMapping` 移至 `ReleaseKit.Common` 或 `ReleaseKit.Application` 層，或在 Application 層定義自己的抽象

**Final Decision**: 基底類別直接接收 `IReadOnlyList<string>` 形式的使用者 ID 清單（由子類別從 `UserMappingOptions` 擷取），避免 Application 層直接依賴 Console 層的 Options 類別。子類別負責從 `UserMappingOptions` 中提取對應平台的 UserId 清單。

### 3. Redis Key 命名

**Decision**: `GitLab:PullRequests:ByUser` 和 `Bitbucket:PullRequests:ByUser`

**Rationale**:
- 使用者在 brainstorming 階段明確選擇此命名格式
- 與現有 Key 命名風格一致（`Platform:Resource:Qualifier`）
- 語意清晰，表達「依使用者過濾後」的含義

**Alternatives considered**:
- `Platform:PullRequests:Filtered` → 較為泛化，未來若有其他過濾條件可能產生歧義
- `Platform:FilteredPullRequests` → 不符合現有命名層級風格

### 4. 空 UserId 處理

**Decision**: 過濾時忽略 `UserMapping.Mappings` 中 UserId 為空字串的項目。

**Rationale**:
- 使用者可能僅設定了單一平台的 UserId（例如只有 GitLabUserId 而無 BitbucketUserId）
- 空字串不應被視為有效的過濾條件，否則可能錯誤匹配到 AuthorUserId 為空的 PR

### 5. 含 Error 的 ProjectResult 處理

**Decision**: 當 `ProjectResult.Error` 不為 null 時，保留該 ProjectResult 原樣，不進行 PR 過濾。

**Rationale**:
- 含 Error 的 ProjectResult 代表先前 fetch 指令執行失敗
- 過濾時不應修改其內容，保持錯誤資訊完整以便下游處理
- 其 PullRequests 清單通常為空，過濾也無意義
