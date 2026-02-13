# Contract: Redis 輸出資料格式

**Feature**: 003-fetch-azure-workitems
**Date**: 2026-02-13
**Direction**: 本系統為寫入端，後續步驟為讀取端

## Redis Key

```
AzureDevOps:WorkItems
```

## TTL

無（不設定過期時間）

## Value Format

JSON 格式，使用 `JsonExtensions.ToJson()` 序列化（camelCase 命名策略）。

```json
{
  "workItems": [
    {
      "workItemId": 12345,
      "title": "修復登入頁面 500 錯誤",
      "type": "Bug",
      "state": "Active",
      "url": "https://dev.azure.com/org/project/_workitems/edit/12345",
      "originalTeamName": "MyProject\\TeamA",
      "isSuccess": true,
      "errorMessage": null
    },
    {
      "workItemId": 99999,
      "title": null,
      "type": null,
      "state": null,
      "url": null,
      "originalTeamName": null,
      "isSuccess": false,
      "errorMessage": "Work Item '99999' 不存在或無權限存取"
    }
  ],
  "totalPRsAnalyzed": 15,
  "totalWorkItemsFound": 8,
  "successCount": 7,
  "failureCount": 1
}
```

## Schema

**Root**: `WorkItemFetchResult`

| 欄位 | 型別 | 必填 | 說明 |
|------|------|------|------|
| workItems | array | Yes | WorkItemOutput 清單 |
| totalPRsAnalyzed | int | Yes | 分析的 PR 總數 |
| totalWorkItemsFound | int | Yes | 解析出的不重複 Work Item ID 總數 |
| successCount | int | Yes | 成功取得資訊的 Work Item 數量 |
| failureCount | int | Yes | 取得失敗的 Work Item 數量 |

**WorkItemOutput**:

| 欄位 | 型別 | 條件 | 說明 |
|------|------|------|------|
| workItemId | int | 必填 | Work Item ID |
| title | string? | 成功時有值 | 標題 |
| type | string? | 成功時有值 | 類型 |
| state | string? | 成功時有值 | 狀態 |
| url | string? | 成功時有值 | 網頁連結 |
| originalTeamName | string? | 成功時有值 | 原始區域路徑 |
| isSuccess | bool | 必填 | 查詢是否成功 |
| errorMessage | string? | 失敗時有值 | 錯誤原因 |

## 輸入依賴

本指令從以下 Redis Key 讀取資料：

| Redis Key | 寫入者 | 格式 |
|-----------|--------|------|
| `GitLab:PullRequests:ByUser` | `filter-gitlab-pr-by-user` 指令 | `FetchResult` JSON |
| `Bitbucket:PullRequests:ByUser` | `filter-bitbucket-pr-by-user` 指令 | `FetchResult` JSON |
