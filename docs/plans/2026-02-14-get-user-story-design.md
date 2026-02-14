# Get User Story 功能設計

## 概述

為 release-kit 新增三項功能：
1. PR 資料結構新增 PR ID 欄位
2. Work Item 抓取邏輯保留 PR 關聯資訊
3. 新增 `get-user-story` console arg，將 Task/Bug 等類型解析至 parent User Story

## 設計決策

- **方案選擇**：分離式任務（方案 A）— 每個需求獨立處理，新功能用獨立 `GetUserStoryTask` 實作
- **理由**：遵循 SRP，不影響現有任務行為，符合專案既有的 Task 模式

---

## 一、PR 資料結構變更

### 目標

在 PR 資料模型中新增 `PullRequestId` 欄位。

### 影響範圍

| 層級 | 檔案 | 變更 |
|------|------|------|
| Domain | `MergeRequest.cs` | 新增 `required int PullRequestId` 屬性 |
| Application | `MergeRequestOutput.cs` | 新增 `int PullRequestId` 屬性 |
| Infrastructure | GitLab Mapper | 從 API response 的 `iid` 欄位映射 |
| Infrastructure | Bitbucket Mapper | 從 API response 的 `id` 欄位映射 |

### 資料流

```
GitLab API (iid) / Bitbucket API (id)
  → Mapper → MergeRequest.PullRequestId
  → Task → MergeRequestOutput.PullRequestId
  → Redis (JSON 序列化)
```

### 備註

GitLab 使用 `iid`（專案內唯一）而非 `id`（全域唯一），因為 `iid` 更符合使用者在 GitLab UI 上看到的 MR 編號。

---

## 二、Work Item 抓取邏輯 — 保留 PR 關聯

### 目標

每筆 work item 記錄直接帶上來源 PR 的資訊，一對一關係，不做去重。

### 修改 WorkItemOutput

新增三個欄位，記錄來源 PR 資訊：

```csharp
public int? SourcePullRequestId { get; init; }
public string? SourceProjectName { get; init; }
public string? SourcePRUrl { get; init; }
```

### 邏輯變更

目前 `ParseVSTSIdsFromPRs` 先去重再逐一查詢。改為：

1. 遍歷每個 `ProjectResult` → 每個 `MergeRequestOutput`
2. 從 PR title 解析 VSTS ID
3. 每對 `(VSTS ID, PR)` 產生一筆記錄，保留 PR 的 `PullRequestId`、`ProjectPath`、`PRUrl`
4. 同一個 Work Item ID 可能出現多筆（來自不同 PR），不做去重
5. 呼叫 `GetWorkItemAsync` 取得 Work Item 詳情，組裝到 `WorkItemOutput` 時帶入 PR 來源欄位

### 資料範例

```json
[
  { "workItemId": 1234, "title": "...", "sourcePullRequestId": 101, "sourceProjectName": "group/api", "sourcePRUrl": "https://..." },
  { "workItemId": 1234, "title": "...", "sourcePullRequestId": 205, "sourceProjectName": "group/web", "sourcePRUrl": "https://..." },
  { "workItemId": 5678, "title": "...", "sourcePullRequestId": 101, "sourceProjectName": "group/api", "sourcePRUrl": "https://..." }
]
```

### 備註

同一個 Work Item ID 出現多次時，只需呼叫一次 API（用快取或先查再填），避免重複 API 呼叫浪費。

---

## 三、新 Console Arg — User Story 解析任務

### 目標

新增 `get-user-story` console arg，從 Redis 讀取 work items，將非 User Story 以上的類型遞迴往上找 parent，直到找到 User Story / Feature / Epic，存入新 Redis key。

### 新增項目一覽

| 層級 | 檔案 | 說明 |
|------|------|------|
| Common | `RedisKeys.cs` | 新增 `AzureDevOpsUserStories` key |
| Application | `TaskType.cs` | 新增 `GetUserStory` enum |
| Application | `UserStoryOutput.cs` | 新 DTO，仿 `WorkItemOutput` + `OriginalWorkItemId` |
| Application | `UserStoryFetchResult.cs` | 新 DTO，仿 `WorkItemFetchResult` |
| Application | `GetUserStoryTask.cs` | 新 Task 實作 |
| Infrastructure | `AzureDevOpsWorkItemResponse.cs` | 新增 `Relations` 欄位解析 |
| Console | `CommandLineParser.cs` | 新增 `"get-user-story"` 映射 |
| Console | `TaskFactory.cs` | 新增 `GetUserStory` case |
| Console | `ServiceCollectionExtensions.cs` | DI 註冊 `GetUserStoryTask` |

### UserStoryOutput DTO

```csharp
public sealed record UserStoryOutput
{
    public required int WorkItemId { get; init; }
    public required int OriginalWorkItemId { get; init; }
    public string? Title { get; init; }
    public string? Type { get; init; }
    public string? State { get; init; }
    public string? Url { get; init; }
    public string? OriginalTeamName { get; init; }
    public required bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
}
```

- 原始 work item 已是 User Story / Feature / Epic → `WorkItemId == OriginalWorkItemId`
- Task/Bug 成功找到 parent → `WorkItemId = parent ID`，`OriginalWorkItemId = 原始 ID`
- 找不到 parent 或 API 失敗 → 保留原始資料，`WorkItemId == OriginalWorkItemId`

### Relations 解析

Azure DevOps API `$expand=all` 已回傳 `relations`，目前未解析。需擴展 response model：

```csharp
// AzureDevOpsWorkItemResponse.cs 新增
[JsonPropertyName("relations")]
public List<AzureDevOpsRelationResponse>? Relations { get; init; }

// 新增 model
public sealed record AzureDevOpsRelationResponse
{
    [JsonPropertyName("rel")]
    public string Rel { get; init; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;
}
```

Parent 關係的 `rel` 值為 `System.LinkTypes.Hierarchy-Reverse`，`url` 格式為 `https://dev.azure.com/{org}/_apis/wit/workItems/{parentId}`，從中提取 parent ID。

### GetUserStoryTask 處理流程

```
1. 從 Redis 讀取 AzureDevOps:WorkItems
2. 清除舊的 AzureDevOps:UserStories
3. 遍歷每筆 WorkItemOutput：
   ├─ IsSuccess == false → 直接保留（OriginalWorkItemId = WorkItemId）
   ├─ Type 是 User Story / Feature / Epic → 直接保留
   └─ Type 是 Task / Bug / 其他：
       ├─ 呼叫 GetWorkItemAsync(WorkItemId) 取得含 relations 的資料
       ├─ 進入迴圈：
       │   ├─ 解析 parent relation → 取得 parent ID
       │   ├─ 呼叫 GetWorkItemAsync(parentId)
       │   ├─ parent Type 是 User Story / Feature / Epic → 結束迴圈，用 parent 資料
       │   ├─ parent Type 不是 → 繼續用 parent 的 relations 往上找
       │   └─ 沒有 parent / API 失敗 → 結束迴圈
       └─ 迴圈結束後：
           ├─ 有找到 US/Feature/Epic → 用該 work item 資料建立 UserStoryOutput
           └─ 沒找到 → 保留原始 work item 資料（OriginalWorkItemId = WorkItemId）
4. 組裝 UserStoryFetchResult 存入 Redis
```

### 安全機制

設定最大遍歷深度（10 層），防止循環參照造成無限迴圈。

### 合格類型定義

以下類型視為「User Story 以上」，不需要往上找 parent：
- User Story
- Feature
- Epic

其他所有類型（Task、Bug 等）都需要遞迴找 parent。
