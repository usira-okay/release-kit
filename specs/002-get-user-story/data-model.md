# Data Model: Azure Work Item User Story Resolution

**Date**: 2026-02-18  
**Feature**: Azure Work Item User Story Resolution

## Entities

### UserStoryWorkItemOutput (NEW)

**Purpose**: 表示經過 User Story 解析處理的 Work Item 資料，包含轉換後的 User Story 資訊與原始 Work Item 資訊

**Location**: `ReleaseKit.Application/Common/UserStoryWorkItemOutput.cs`

**Fields**:

| 欄位名稱 | 型別 | 必填 | 說明 | 驗證規則 |
|---------|------|------|------|---------|
| `workItemId` | `int` | ✅ | 轉換後的 Work Item ID（User Story 的 ID） | > 0 |
| `title` | `string?` | ❌ | 轉換後的標題 | 失敗時為 null |
| `type` | `string?` | ❌ | 轉換後的類型（應為 User Story/Feature/Epic） | 失敗時為 null |
| `state` | `string?` | ❌ | 轉換後的狀態 | 失敗時為 null |
| `url` | `string?` | ❌ | 轉換後的 URL | 失敗時為 null |
| `originalTeamName` | `string?` | ❌ | 原始團隊名稱 | 失敗時為 null |
| `isSuccess` | `bool` | ✅ | 是否成功取得資訊 | true/false |
| `errorMessage` | `string?` | ❌ | 失敗時的錯誤原因 | 成功時為 null |
| `resolutionStatus` | `UserStoryResolutionStatus` | ✅ | 解析狀態 | enum 值 |
| `originalWorkItem` | `WorkItemOutput?` | ❌ | 原始 Work Item 資訊 | 若無轉換則為 null |

**Relationships**:
- 繼承/擴充自 `WorkItemOutput` 的概念（使用組合而非繼承）
- `originalWorkItem` 參照 `WorkItemOutput` 型別

**State Transitions**: N/A（唯讀 DTO）

**JSON Example**:

```json
{
  "workItemId": 67890,
  "title": "新增使用者登入功能",
  "type": "User Story",
  "state": "Active",
  "url": "https://dev.azure.com/org/proj/_workitems/edit/67890",
  "originalTeamName": "Platform/Web",
  "isSuccess": true,
  "errorMessage": null,
  "resolutionStatus": "foundViaRecursion",
  "originalWorkItem": {
    "workItemId": 12345,
    "title": "修正登入按鈕顏色",
    "type": "Bug",
    "state": "Resolved",
    "url": "https://dev.azure.com/org/proj/_workitems/edit/12345",
    "originalTeamName": "Platform/Web",
    "isSuccess": true,
    "errorMessage": null
  }
}
```

---

### UserStoryResolutionStatus (NEW)

**Purpose**: 表示 Work Item 的 User Story 解析結果

**Location**: `ReleaseKit.Application/Common/UserStoryResolutionStatus.cs`

**Type**: Enum

**Values**:

| 值 | 說明 | 使用時機 |
|----|------|---------|
| `AlreadyUserStoryOrAbove` | 原始 Type 就是 User Story 或以上的類型 | 原始 Work Item 的 Type 為 User Story/Feature/Epic |
| `FoundViaRecursion` | 透過遞迴找到 User Story 或以上的類型 | 成功透過 Parent 查詢找到 User Story 層級 |
| `NotFound` | 無法找到 User Story 或以上的類型 | 遞迴過程中失敗、達到最大深度、偵測到循環、無 Parent |
| `OriginalFetchFailed` | 原始的 Work Item 就無法取得資訊 | 原始 Work Item API 呼叫失敗（404、401 等） |

**JSON Serialization**: 
- 序列化為 camelCase 字串（透過 `JsonStringEnumConverter`）
- Example: `"alreadyUserStoryOrAbove"`, `"foundViaRecursion"`

---

### UserStoryFetchResult (NEW)

**Purpose**: Work Item User Story 解析結果彙整 DTO

**Location**: `ReleaseKit.Application/Common/UserStoryFetchResult.cs`

**Fields**:

| 欄位名稱 | 型別 | 必填 | 說明 |
|---------|------|------|------|
| `workItems` | `List<UserStoryWorkItemOutput>` | ✅ | 所有解析結果清單 |
| `totalWorkItems` | `int` | ✅ | 原始 Work Item 總數 |
| `alreadyUserStoryCount` | `int` | ✅ | 原本就是 User Story 層級的數量 |
| `foundViaRecursionCount` | `int` | ✅ | 透過遞迴找到的數量 |
| `notFoundCount` | `int` | ✅ | 無法找到 User Story 的數量 |
| `fetchFailedCount` | `int` | ✅ | 原始資料取得失敗的數量 |

**JSON Example**:

```json
{
  "workItems": [ ... ],
  "totalWorkItems": 100,
  "alreadyUserStoryCount": 30,
  "foundViaRecursionCount": 60,
  "notFoundCount": 8,
  "fetchFailedCount": 2
}
```

---

### WorkItemTypeConstants (NEW)

**Purpose**: 定義 Work Item 類型判斷常數

**Location**: `ReleaseKit.Common/Constants/WorkItemTypeConstants.cs`

**Constants**:

```csharp
public static class WorkItemTypeConstants
{
    /// <summary>
    /// User Story 層級或以上的類型清單（大小寫不敏感）
    /// </summary>
    public static readonly HashSet<string> UserStoryLevelTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "User Story",
        "Feature",
        "Epic"
    };

    /// <summary>
    /// 判斷 Work Item 類型是否為 User Story 層級或以上
    /// </summary>
    public static bool IsUserStoryLevel(string type) 
        => UserStoryLevelTypes.Contains(type);
}
```

---

### AzureDevOpsWorkItemResponse (MODIFY)

**Purpose**: 擴充現有模型以支援 Relations 欄位

**Location**: `ReleaseKit.Infrastructure/AzureDevOps/Models/AzureDevOpsWorkItemResponse.cs`

**New Fields**:

| 欄位名稱 | 型別 | 說明 |
|---------|------|------|
| `Relations` | `List<AzureDevOpsRelationResponse>?` | Work Item 關聯清單 |

**New Model**:

```csharp
public sealed record AzureDevOpsRelationResponse
{
    [JsonPropertyName("rel")]
    public string Rel { get; init; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("attributes")]
    public Dictionary<string, object?>? Attributes { get; init; }
}
```

---

### RedisKeys (MODIFY)

**Purpose**: 新增 User Story Work Items 的 Redis Key 常數

**Location**: `ReleaseKit.Common/Constants/RedisKeys.cs`

**New Constant**:

```csharp
/// <summary>
/// Azure DevOps User Story Work Items 資料的 Redis Key
/// </summary>
public const string AzureDevOpsUserStoryWorkItems = "AzureDevOps:WorkItems:UserStories";
```

---

## Validation Rules

### UserStoryWorkItemOutput Validation

- `workItemId` 必須 > 0
- 若 `isSuccess = true`，則 `title`、`type`、`state`、`url`、`originalTeamName` 不可為 null
- 若 `isSuccess = false`，則 `errorMessage` 不可為 null
- 若 `resolutionStatus = AlreadyUserStoryOrAbove`，則 `originalWorkItem` 必須為 null
- 若 `resolutionStatus = FoundViaRecursion`，則 `originalWorkItem` 不可為 null
- 若 `resolutionStatus = NotFound` 且原始資料可取得，則 `originalWorkItem` 不可為 null

### UserStoryResolutionStatus Usage

| resolutionStatus | isSuccess | originalWorkItem | 說明 |
|-----------------|-----------|------------------|------|
| `AlreadyUserStoryOrAbove` | `true` | `null` | 原始就是 User Story |
| `FoundViaRecursion` | `true` | 非 `null` | 成功找到 User Story |
| `NotFound` | `true` | 非 `null` | 原始資料可取得但無法找到 Parent |
| `NotFound` | `false` | 可能為 `null` | Parent API 失敗 |
| `OriginalFetchFailed` | `false` | `null` | 原始資料就無法取得 |

## Migration Notes

- 無資料庫 Migration（僅使用 Redis 作為快取）
- 舊的 Redis Key (`AzureDevOps:WorkItems`) 保持不變，不影響現有功能
- 新的 Redis Key (`AzureDevOps:WorkItems:UserStories`) 獨立儲存，可與舊資料共存
