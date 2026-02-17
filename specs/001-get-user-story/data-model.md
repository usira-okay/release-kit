# Data Model: 取得 User Story 層級資訊

**Feature Branch**: `001-get-user-story`
**Date**: 2026-02-17

## 實體變更

### 既有實體擴充

#### WorkItem（Domain Entity）

新增欄位：

| 欄位 | 型別 | 必填 | 說明 |
|------|------|------|------|
| ParentWorkItemId | int? | 否 | 父層級 Work Item 的 ID，從 Azure DevOps API 的 relations 中解析 |

> 注意：此欄位為 nullable，因為頂層 Work Item（如 Epic）沒有 Parent。

---

### 新增實體

#### UserStoryResolutionStatus（Enum）

表示 Work Item 的 User Story 解析結果狀態。

| 值 | 名稱 | 說明 |
|----|------|------|
| 0 | AlreadyUserStoryOrAbove | 原始 Type 即為 User Story 以上的類型 |
| 1 | FoundViaRecursion | 透過遞迴找到 User Story 以上的類型 |
| 2 | NotFound | 無法找到 User Story 以上的類型 |
| 3 | OriginalFetchFailed | 原始的 Work Item 就無法取得資訊 |

---

#### UserStoryResolutionOutput（Application DTO）

代表單一 Work Item 經過 User Story 解析後的結果。

| 欄位 | 型別 | 必填 | 說明 |
|------|------|------|------|
| WorkItemId | int | 是 | 原始 Work Item ID |
| Title | string? | 否 | 原始 Work Item 標題（取得失敗時為 null） |
| Type | string? | 否 | 原始 Work Item 類型（取得失敗時為 null） |
| State | string? | 否 | 原始 Work Item 狀態（取得失敗時為 null） |
| Url | string? | 否 | 原始 Work Item 網址（取得失敗時為 null） |
| OriginalTeamName | string? | 否 | 原始 Work Item 團隊名稱（取得失敗時為 null） |
| IsSuccess | bool | 是 | 原始 Work Item 是否取得成功 |
| ErrorMessage | string? | 否 | 原始 Work Item 取得失敗時的錯誤訊息 |
| ResolutionStatus | UserStoryResolutionStatus | 是 | 解析結果狀態 |
| UserStory | UserStoryInfo? | 否 | 找到的 User Story 資訊（僅 AlreadyUserStoryOrAbove 與 FoundViaRecursion 時有值） |

---

#### UserStoryInfo（Application DTO）

代表透過解析找到的 User Story（或更高層級）資訊。

| 欄位 | 型別 | 必填 | 說明 |
|------|------|------|------|
| WorkItemId | int | 是 | User Story 的 Work Item ID |
| Title | string | 是 | User Story 標題 |
| Type | string | 是 | Work Item 類型（User Story / Feature / Epic） |
| State | string | 是 | Work Item 狀態 |
| Url | string | 是 | Work Item 網址 |

---

#### UserStoryResolutionResult（Application DTO）

彙總所有 Work Item 的 User Story 解析結果。

| 欄位 | 型別 | 必填 | 說明 |
|------|------|------|------|
| Items | List\<UserStoryResolutionOutput\> | 是 | 所有解析結果 |
| TotalCount | int | 是 | 總 Work Item 數量 |
| AlreadyUserStoryCount | int | 是 | 原始即為 User Story 以上的數量 |
| FoundViaRecursionCount | int | 是 | 透過遞迴找到的數量 |
| NotFoundCount | int | 是 | 無法找到的數量 |
| OriginalFetchFailedCount | int | 是 | 原始取得失敗的數量 |

---

### 基礎設施層模型擴充

#### AzureDevOpsWorkItemResponse（新增 Relations 欄位）

新增欄位以接收 API 回傳的關聯資料：

| 欄位 | 型別 | 必填 | 說明 |
|------|------|------|------|
| Relations | List\<AzureDevOpsRelationResponse\>? | 否 | Work Item 的關聯清單 |

---

#### AzureDevOpsRelationResponse（新增模型）

代表 Azure DevOps API 回傳的單一關聯。

| 欄位 | 型別 | 必填 | 說明 |
|------|------|------|------|
| Rel | string | 是 | 關聯類型（如 `System.LinkTypes.Hierarchy-Reverse` 表示 Parent） |
| Url | string | 是 | 關聯目標的 API URL |
| Attributes | Dictionary\<string, object?\>? | 否 | 關聯的屬性 |

> Parent 關聯識別：`Rel == "System.LinkTypes.Hierarchy-Reverse"`
> Parent ID 解析：從 `Url` 末尾的數字提取（如 `.../workitems/12345` → `12345`）

---

## 狀態轉換

```
WorkItem 載入
    │
    ├─ IsSuccess == false ──────────→ OriginalFetchFailed
    │
    ├─ Type ∈ {User Story, Feature, Epic}
    │       ──────────→ AlreadyUserStoryOrAbove（UserStory = 自身資訊）
    │
    └─ Type ∉ {User Story, Feature, Epic}
            │
            ├─ 遞迴找到 Parent 且 Type ∈ 目標集合
            │       ──────────→ FoundViaRecursion（UserStory = Parent 資訊）
            │
            └─ 遞迴失敗（無 Parent / API 失敗 / 循環 / 超過深度）
                    ──────────→ NotFound
```

## 常數定義

### Redis Keys

| 常數名稱 | 值 | 說明 |
|----------|-----|------|
| AzureDevOpsUserStories | `AzureDevOps:WorkItems:UserStories` | User Story 解析結果的 Redis Key |

### Work Item Type 常數

| 常數名稱 | 值 | 說明 |
|----------|-----|------|
| UserStoryOrAboveTypes | `{"User Story", "Feature", "Epic"}` | 視為 User Story 以上的類型集合 |

### 遞迴限制

| 常數名稱 | 值 | 說明 |
|----------|-----|------|
| MaxRecursionDepth | 10 | 遞迴查找 Parent 的最大深度 |
