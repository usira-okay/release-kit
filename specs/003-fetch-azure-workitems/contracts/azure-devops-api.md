# Contract: Azure DevOps REST API（消費端）

**Feature**: 003-fetch-azure-workitems
**Date**: 2026-02-13
**Direction**: 本系統為消費端（Client），Azure DevOps 為提供端（Server）

## Endpoint

```
GET {OrganizationUrl}/_apis/wit/workitems/{id}?$expand=all&api-version=7.0
```

## Authentication

- **Method**: Basic Auth
- **Username**: （空字串）
- **Password**: Personal Access Token (PAT)
- **Header**: `Authorization: Basic {Base64Encode(":" + PAT)}`

## Request

| Parameter | Location | Type | Required | Description |
|-----------|----------|------|----------|-------------|
| OrganizationUrl | Base URL | string | Yes | Azure DevOps 組織 URL（來自 AzureDevOpsOptions） |
| id | Path | int | Yes | Work Item ID |
| $expand | Query | string | Yes | 固定值 `all`，展開所有欄位 |
| api-version | Query | string | Yes | 固定值 `7.0` |

## Response (200 OK)

```json
{
  "id": 12345,
  "rev": 3,
  "fields": {
    "System.Title": "修復登入頁面 500 錯誤",
    "System.WorkItemType": "Bug",
    "System.State": "Active",
    "System.AreaPath": "MyProject\\TeamA",
    "System.IterationPath": "MyProject\\Sprint 1",
    "System.Description": "..."
  },
  "_links": {
    "self": {
      "href": "https://dev.azure.com/org/_apis/wit/workitems/12345"
    },
    "html": {
      "href": "https://dev.azure.com/org/project/_workitems/edit/12345"
    }
  },
  "url": "https://dev.azure.com/org/_apis/wit/workitems/12345"
}
```

## 使用的欄位

| JSON 路徑 | 用途 | 對應 Domain 欄位 |
|----------|------|-----------------|
| `id` | Work Item ID | `WorkItem.WorkItemId` |
| `fields["System.Title"]` | 標題 | `WorkItem.Title` |
| `fields["System.WorkItemType"]` | 類型 | `WorkItem.Type` |
| `fields["System.State"]` | 狀態 | `WorkItem.State` |
| `_links.html.href` | 網頁連結 | `WorkItem.Url` |
| `fields["System.AreaPath"]` | 區域路徑 | `WorkItem.OriginalTeamName` |

## Error Responses

| HTTP Status | Error Code | 處理方式 |
|-------------|-----------|---------|
| 401 Unauthorized | — | `Error.AzureDevOps.Unauthorized` |
| 404 Not Found | — | `Error.AzureDevOps.WorkItemNotFound(id)` |
| 其他非 2xx | — | `Error.AzureDevOps.ApiError("HTTP {statusCode}")` |
