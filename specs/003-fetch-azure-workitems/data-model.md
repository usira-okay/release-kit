# Data Model: Azure DevOps Work Item 資訊擷取

**Feature**: 003-fetch-azure-workitems
**Date**: 2026-02-13

## Domain Layer Entities

### WorkItem

**位置**: `src/ReleaseKit.Domain/Entities/WorkItem.cs`
**類型**: Sealed Record（不可變領域實體）

| 欄位 | 型別 | 必填 | 說明 | 來源 |
|------|------|------|------|------|
| WorkItemId | int | required | Azure DevOps Work Item 唯一識別碼 | API `id` |
| Title | string | required | 工作項目標題 | API `fields["System.Title"]` |
| Type | string | required | 工作項目類型（Bug/Task/User Story） | API `fields["System.WorkItemType"]` |
| State | string | required | 工作項目狀態（New/Active/Resolved/Closed） | API `fields["System.State"]` |
| Url | string | required | 工作項目網頁連結 | API `_links.html.href` |
| OriginalTeamName | string | required | 原始區域路徑（本階段不做 TeamMapping 轉換） | API `fields["System.AreaPath"]` |

**驗證規則**: 無（所有欄位由外部 API 提供，透過 Mapper 映射時確保完整性）

---

## Application Layer DTOs

### WorkItemOutput

**位置**: `src/ReleaseKit.Application/Common/WorkItemOutput.cs`
**類型**: Sealed Record（輸出 DTO）

| 欄位 | 型別 | 說明 |
|------|------|------|
| WorkItemId | int | Work Item 識別碼 |
| Title | string? | 標題（失敗時為 null） |
| Type | string? | 類型（失敗時為 null） |
| State | string? | 狀態（失敗時為 null） |
| Url | string? | 連結 URL（失敗時為 null） |
| OriginalTeamName | string? | 原始區域路徑（失敗時為 null） |
| IsSuccess | bool | 是否成功取得 Work Item 資訊 |
| ErrorMessage | string? | 失敗時的錯誤原因（成功時為 null） |

**狀態轉換**:
- 成功: `IsSuccess = true`，所有欄位填入，`ErrorMessage = null`
- 失敗: `IsSuccess = false`，僅 `WorkItemId` 與 `ErrorMessage` 有值，其餘為 null

### WorkItemFetchResult

**位置**: `src/ReleaseKit.Application/Common/WorkItemFetchResult.cs`
**類型**: Sealed Record（彙整結果 DTO）

| 欄位 | 型別 | 說明 |
|------|------|------|
| WorkItems | List\<WorkItemOutput\> | 所有 Work Item 查詢結果清單 |
| TotalPRsAnalyzed | int | 分析的 PR 總數 |
| TotalWorkItemsFound | int | 解析出的不重複 Work Item ID 總數 |
| SuccessCount | int | 成功取得資訊的 Work Item 數量 |
| FailureCount | int | 取得失敗的 Work Item 數量 |

---

## Infrastructure Layer Models

### AzureDevOpsWorkItemResponse

**位置**: `src/ReleaseKit.Infrastructure/AzureDevOps/Models/AzureDevOpsWorkItemResponse.cs`
**類型**: Sealed Record（API 反序列化模型）
**用途**: 反序列化 Azure DevOps REST API 回傳的 JSON

| 欄位 | JSON 路徑 | 型別 | JsonPropertyName |
|------|----------|------|-----------------|
| Id | `id` | int | `id` |
| Fields | `fields` | Dictionary\<string, object?\> | `fields` |
| Links | `_links` | AzureDevOpsLinksResponse? | `_links` |

**注意**: 使用 `[JsonPropertyName]` 是因為這是外部 API 契約，符合 Constitution IX 例外條款。

### AzureDevOpsLinksResponse

**位置**: 同上（巢狀類別或獨立檔案）
**類型**: Sealed Record

| 欄位 | JSON 路徑 | 型別 | JsonPropertyName |
|------|----------|------|-----------------|
| Html | `html` | AzureDevOpsLinkResponse? | `html` |

### AzureDevOpsLinkResponse

**位置**: 同上
**類型**: Sealed Record

| 欄位 | JSON 路徑 | 型別 | JsonPropertyName |
|------|----------|------|-----------------|
| Href | `href` | string | `href` |

---

## Domain Layer Error Extensions

### Error.AzureDevOps

**位置**: `src/ReleaseKit.Domain/Common/Error.cs`（修改既有檔案）

| 錯誤方法 | 錯誤碼 | 訊息 |
|---------|--------|------|
| WorkItemNotFound(int workItemId) | `AzureDevOps.WorkItemNotFound` | `Work Item '{workItemId}' 不存在或無權限存取` |
| ApiError(string message) | `AzureDevOps.ApiError` | `Azure DevOps API 呼叫失敗：{message}` |
| Unauthorized (static) | `AzureDevOps.Unauthorized` | `Azure DevOps API 驗證失敗，請檢查 Personal Access Token` |

---

## Domain Layer Abstractions

### IAzureDevOpsRepository

**位置**: `src/ReleaseKit.Domain/Abstractions/IAzureDevOpsRepository.cs`

| 方法 | 回傳型別 | 說明 |
|------|---------|------|
| GetWorkItemAsync(int workItemId) | Task\<Result\<WorkItem\>\> | 取得單一 Work Item 詳細資訊 |

---

## Entity Relationships

```
FetchResult (Redis 輸入)
  └── ProjectResult (1:N)
        └── MergeRequestOutput (1:N)
              └── Title → VSTS(\d+) 正則解析 → WorkItem ID (N:M)

WorkItem ID → IAzureDevOpsRepository.GetWorkItemAsync()
  └── 成功: WorkItem → WorkItemOutput (IsSuccess=true)
  └── 失敗: Error → WorkItemOutput (IsSuccess=false)

List<WorkItemOutput> → WorkItemFetchResult → Redis 輸出
```

---

## Constants Extensions

### RedisKeys（修改）

| 常數 | 值 | 說明 |
|------|---|------|
| AzureDevOpsWorkItems | `"AzureDevOps:WorkItems"` | 輸出結果 Redis Key |

### HttpClientNames（修改）

| 常數 | 值 | 說明 |
|------|---|------|
| AzureDevOps | `"AzureDevOps"` | Named HttpClient 名稱 |
