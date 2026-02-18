# Azure DevOps Work Item API Contract

**Purpose**: 定義與 Azure DevOps REST API 的整合契約，特別是 Work Item Relations（Parent 查詢）

**API Version**: 7.0  
**Base URL**: `https://dev.azure.com/{organization}/{project}`

## Endpoint

### GET Work Item with Relations

```http
GET /_apis/wit/workitems/{id}?$expand=all&api-version=7.0
Authorization: Basic {PAT_Base64}
```

**Parameters**:
- `{id}`: Work Item ID (integer)
- `$expand=all`: 展開所有欄位，包含 relations

**Response 200 OK**:

```json
{
  "id": 12345,
  "rev": 10,
  "fields": {
    "System.Title": "修正登入按鈕顏色",
    "System.WorkItemType": "Bug",
    "System.State": "Resolved",
    "System.AreaPath": "Platform\\Web",
    "System.TeamProject": "MyProject",
    "System.CreatedDate": "2026-01-15T10:30:00Z",
    "System.ChangedDate": "2026-02-10T14:20:00Z"
  },
  "_links": {
    "self": {
      "href": "https://dev.azure.com/org/proj/_apis/wit/workitems/12345"
    },
    "html": {
      "href": "https://dev.azure.com/org/proj/_workitems/edit/12345"
    }
  },
  "relations": [
    {
      "rel": "System.LinkTypes.Hierarchy-Reverse",
      "url": "https://dev.azure.com/org/proj/_apis/wit/workitems/67890",
      "attributes": {
        "isLocked": false,
        "name": "Parent"
      }
    },
    {
      "rel": "System.LinkTypes.Hierarchy-Forward",
      "url": "https://dev.azure.com/org/proj/_apis/wit/workitems/11111",
      "attributes": {
        "isLocked": false,
        "name": "Child"
      }
    }
  ]
}
```

## Relations Field Structure

### Relation Types

| Rel Type | 方向 | 說明 |
|----------|------|------|
| `System.LinkTypes.Hierarchy-Reverse` | Parent | 此 Work Item 的上層（Parent） |
| `System.LinkTypes.Hierarchy-Forward` | Child | 此 Work Item 的下層（Child） |
| `System.LinkTypes.Related` | N/A | 相關的 Work Item（非階層關係） |

### Parent Relation Structure

```json
{
  "rel": "System.LinkTypes.Hierarchy-Reverse",
  "url": "https://dev.azure.com/{organization}/{project}/_apis/wit/workitems/{parentId}",
  "attributes": {
    "isLocked": false,
    "name": "Parent"
  }
}
```

**Key Fields**:
- `rel`: 必須為 `"System.LinkTypes.Hierarchy-Reverse"` 才是 Parent
- `url`: 包含 Parent Work Item 的 API 端點
- `url` 格式: `https://dev.azure.com/{org}/{proj}/_apis/wit/workitems/{parentId}`

### Extracting Parent ID

**URL Pattern**: 
```regex
workitems/(\d+)$
```

**Example**:
```
Input:  https://dev.azure.com/org/proj/_apis/wit/workitems/67890
Output: 67890
```

**C# Implementation**:
```csharp
var match = Regex.Match(url, @"workitems/(\d+)$");
if (match.Success && int.TryParse(match.Groups[1].Value, out var parentId))
{
    return parentId;
}
```

## Error Responses

### 404 Not Found

Work Item 不存在或已刪除

```json
{
  "id": "0",
  "innerException": null,
  "message": "TF401232: Work item 12345 does not exist, or you do not have permissions to read it.",
  "typeName": "Microsoft.TeamFoundation.WorkItemTracking.Server.WorkItemNotFoundException",
  "typeKey": "WorkItemNotFoundException",
  "errorCode": 0,
  "eventId": 3000
}
```

### 401 Unauthorized

認證失敗或 PAT Token 過期

```json
{
  "Message": "TF400813: The user '{user}' is not authorized to access this resource."
}
```

### 429 Too Many Requests

超過速率限制（預設 200 requests/min）

```http
HTTP/1.1 429 Too Many Requests
Retry-After: 60
```

## Edge Cases

### No Relations

若 Work Item 沒有任何關聯，`relations` 欄位可能為：
- `null`
- `[]` (空陣列)

**Handling**:
```csharp
var relations = response.Relations ?? new List<AzureDevOpsRelationResponse>();
var parentRelation = relations.FirstOrDefault(r => r.Rel == "System.LinkTypes.Hierarchy-Reverse");
if (parentRelation is null)
{
    // 沒有 Parent，停止遞迴
}
```

### Multiple Parents

**不應發生**: Azure DevOps 的階層關係保證一個 Work Item 最多只有一個 Parent

**Defensive Handling**: 若偵測到多個 Parent Relation，使用第一個並記錄警告

```csharp
var parentRelations = relations
    .Where(r => r.Rel == "System.LinkTypes.Hierarchy-Reverse")
    .ToList();

if (parentRelations.Count > 1)
{
    _logger.LogWarning("Work Item {WorkItemId} has {Count} parent relations, using first one", 
        workItemId, parentRelations.Count);
}

var parentRelation = parentRelations.FirstOrDefault();
```

### Malformed URL

Parent URL 格式錯誤，無法解析 Parent ID

**Handling**:
```csharp
var match = Regex.Match(url, @"workitems/(\d+)$");
if (!match.Success)
{
    _logger.LogError("Failed to extract parent ID from URL: {Url}", url);
    return NotFound; // 設定 resolutionStatus 為 NotFound
}
```

## Rate Limiting

**Azure DevOps Limits**:
- 預設: 200 requests/minute per user
- 若超過限制，回傳 HTTP 429
- `Retry-After` header 指示重試等待時間（秒）

**Strategy**:
- 不實作自動重試（YAGNI）
- 將速率限制錯誤記錄為 `isSuccess: false`
- 錯誤訊息包含 "Rate limit exceeded"

## Security

**PAT Token Management**:
- Token 儲存於環境變數或 User Secrets
- 禁止在程式碼中硬編碼 Token
- Token 需要 `Work Items (Read)` 權限

**Configuration Example**:

```json
{
  "AzureDevOps": {
    "OrganizationUrl": "https://dev.azure.com/myorg",
    "ProjectName": "MyProject",
    "PersonalAccessToken": "${AZURE_DEVOPS_PAT}"
  }
}
```

## Performance Considerations

**Caching Strategy** (Future Enhancement):
- 目前不實作 Work Item 快取（YAGNI）
- 若未來需要優化，可考慮：
  - 使用 Redis 快取 Parent 查詢結果
  - TTL 設定為 5-10 分鐘
  - Cache Key: `AzureDevOps:WorkItem:Parent:{workItemId}`

**Batch API** (Future Enhancement):
- Azure DevOps 支援 Batch API 一次查詢多個 Work Item
- 目前規模（數十至數百筆）不需要 Batch 優化
- 若未來需要處理數千筆，可考慮使用 Batch API

```http
POST /_apis/wit/workitemsbatch?api-version=7.0
Content-Type: application/json

{
  "ids": [12345, 67890, 11111],
  "$expand": "all"
}
```
