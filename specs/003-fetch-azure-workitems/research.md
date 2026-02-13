# Research: Azure DevOps Work Item 資訊擷取

**Feature**: 003-fetch-azure-workitems
**Date**: 2026-02-13

## R1: Azure DevOps REST API Work Item 端點

**Decision**: 使用 `GET _apis/wit/workitems/{id}?$expand=all&api-version=7.0` 端點逐一查詢 Work Item

**Rationale**:
- 單一 Work Item 端點最直接，語意清晰
- `$expand=all` 一次取得所有欄位（fields、relations、links），避免多次呼叫
- API v7.0 為穩定版本
- 雖然有批次端點 `_apis/wit/workitems?ids=1,2,3`，但本階段決策為循序呼叫，批次端點為未來優化選項

**Alternatives considered**:
- WIQL 查詢：過度複雜，僅需以 ID 查詢不需要 WIQL
- 批次端點 `?ids=1,2,3`：可一次查詢多個 ID，但本階段採循序呼叫，且需處理批次部分失敗的複雜邏輯

## R2: Azure DevOps API 認證方式

**Decision**: 使用 Basic Auth，username 為空字串，password 為 Personal Access Token (PAT)

**Rationale**:
- 既有 `AzureDevOpsOptions` 已包含 `PersonalAccessToken` 欄位
- Basic Auth 是 Azure DevOps REST API 最簡單的認證方式
- PAT 已透過設定檔管理，無硬編碼風險
- Authorization Header 格式：`Basic {Base64(":" + PAT)}`

**Alternatives considered**:
- OAuth2：需要註冊 Azure AD 應用程式，過度複雜
- Azure Identity (Managed Identity)：適用於 Azure 託管環境，本專案為 Docker Container 不適用

## R3: API Response 欄位對應

**Decision**: 從 Azure DevOps API 回應中擷取以下欄位

| API Response 路徑 | Domain Entity 欄位 | 說明 |
|---|---|---|
| `id` | `WorkItemId` | Work Item 唯一識別碼 |
| `fields["System.Title"]` | `Title` | 標題 |
| `fields["System.WorkItemType"]` | `Type` | 類型（Bug/Task/User Story） |
| `fields["System.State"]` | `State` | 狀態（New/Active/Resolved/Closed） |
| `_links.html.href` | `Url` | Work Item 網頁連結 |
| `fields["System.AreaPath"]` | `OriginalTeamName` | 區域路徑（本階段直接儲存，不做 mapping） |

**Rationale**: 這些欄位涵蓋 Release Notes 所需的核心資訊，`$expand=all` 確保 `_links` 可用

**Alternatives considered**: 無，欄位選擇由 spec 明確定義

## R4: VSTS ID 正規表達式

**Decision**: 使用 `VSTS(\d+)` 正規表達式，case-sensitive（僅匹配大寫）

**Rationale**:
- 既有慣例中 VSTS ID 格式固定為大寫 `VSTS` 後接數字
- Case-sensitive 避免誤匹配（如 `vsts` 在一般描述文字中可能出現）
- `\d+` 匹配一位或多位數字，不限制長度

**Alternatives considered**:
- Case-insensitive 匹配：風險過高，可能匹配到非 ID 的文字
- `VSTS\d{5,6}`（限制位數）：過度約束，可能遺漏合法 ID

## R5: PR 資料 Redis 格式

**Decision**: 從 Redis 讀取的 PR 資料為 `FetchResult` 型別的 JSON，內含 `List<ProjectResult>`，每個 `ProjectResult` 包含 `List<MergeRequestOutput>`

**Rationale**:
- 確認既有 `FilterGitLabPullRequestsByUserTask` 和 `FilterBitbucketPullRequestsByUserTask` 輸出格式為 `FetchResult`
- `MergeRequestOutput.Title` 欄位即為 PR 標題，用於 VSTS ID 解析

**Alternatives considered**: 無，格式由既有實作決定

## R6: 錯誤處理策略

**Decision**: 使用 Result Pattern，個別 Work Item 查詢失敗記錄為 `WorkItemOutput.IsSuccess = false`，不中斷整體流程

**Rationale**:
- 遵循 Constitution V（禁止 try-catch，使用 Result Pattern）
- 個別失敗不應影響其他 Work Item 的查詢
- 失敗記錄保留 WorkItemId 與 ErrorMessage，便於除錯

**Alternatives considered**:
- 中斷處理：違反 spec FR-007，不符合容錯需求
- 重試機制：spec 已確認不需要重試

## R7: HttpClient 註冊模式

**Decision**: 使用 `IHttpClientFactory` Named Client 模式，名稱為 `HttpClientNames.AzureDevOps`

**Rationale**:
- 遵循既有 GitLab/Bitbucket 的 Named Client 模式
- Base Address 從 `AzureDevOpsOptions.OrganizationUrl` 取得
- Authorization Header 在 HttpClient 配置時統一設定

**Alternatives considered**:
- Typed Client：增加一個額外類別，對本需求而言過度設計
- 手動建立 HttpClient：違反 .NET 最佳實踐，可能造成 socket exhaustion

## R8: 既有元件重用清單

**Decision**: 最大化重用既有元件，減少新增程式碼量

| 既有元件 | 重用方式 |
|---------|---------|
| `ITask` | FetchAzureDevOpsWorkItemsTask 實作此介面 |
| `IRedisService` | GetAsync 讀取 PR 資料、SetAsync 寫入結果 |
| `Result<T>` | IAzureDevOpsRepository.GetWorkItemAsync 回傳型別 |
| `Error` | 擴充 AzureDevOps 靜態類別 |
| `JsonExtensions` | ToJson() 序列化輸出、ToTypedObject<T>() 反序列化 Redis 資料 |
| `RedisKeys` | 使用既有 key 讀取 PR，新增 key 寫入 Work Item |
| `HttpClientNames` | 新增 AzureDevOps 常數 |
| `AzureDevOpsOptions` | 讀取 OrganizationUrl 與 PAT |
| `FetchResult` / `MergeRequestOutput` | 從 Redis 反序列化 PR 資料 |

**Rationale**: 遵循 Constitution Plan 指令規範「優先搜尋現有程式碼庫中的相關邏輯，優先重複使用現有元件」
