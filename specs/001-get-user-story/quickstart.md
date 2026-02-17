# Quickstart: 取得 User Story 層級資訊

**Feature Branch**: `001-get-user-story`
**Date**: 2026-02-17

## 前置條件

1. Redis 中已有 Work Item 資料（透過 `fetch-azure-workitems` 指令產生）
2. Azure DevOps 設定已完成（`appsettings.json` 中的 `AzureDevOps` 區塊）

## 執行流程

### 步驟一：確保 Work Item 資料已載入

```bash
dotnet run --project src/ReleaseKit.Console -- fetch-azure-workitems
```

此步驟會將 Work Item 資料寫入 Redis Key `AzureDevOps:WorkItems`。

### 步驟二：執行 User Story 解析

```bash
dotnet run --project src/ReleaseKit.Console -- get-user-story
```

此指令會：
1. 從 Redis 讀取 `AzureDevOps:WorkItems` 中的 Work Item 資料
2. 對每個 Work Item 判斷是否已為 User Story 以上層級
3. 若不是，透過 Azure DevOps API 遞迴查找 Parent 直到找到 User Story 或更高層級
4. 將所有結果（含統計資訊）寫入 Redis Key `AzureDevOps:WorkItems:UserStories`
5. 輸出 JSON 結果至 stdout

## 輸出範例

```json
{
  "items": [
    {
      "workItemId": 12345,
      "title": "實作使用者登入功能",
      "type": "User Story",
      "state": "Active",
      "url": "https://dev.azure.com/org/project/_workitems/edit/12345",
      "originalTeamName": "TeamA",
      "isSuccess": true,
      "errorMessage": null,
      "resolutionStatus": "alreadyUserStoryOrAbove",
      "userStory": {
        "workItemId": 12345,
        "title": "實作使用者登入功能",
        "type": "User Story",
        "state": "Active",
        "url": "https://dev.azure.com/org/project/_workitems/edit/12345"
      }
    },
    {
      "workItemId": 67890,
      "title": "修復登入頁面 CSS 問題",
      "type": "Bug",
      "state": "Resolved",
      "url": "https://dev.azure.com/org/project/_workitems/edit/67890",
      "originalTeamName": "TeamA",
      "isSuccess": true,
      "errorMessage": null,
      "resolutionStatus": "foundViaRecursion",
      "userStory": {
        "workItemId": 12345,
        "title": "實作使用者登入功能",
        "type": "User Story",
        "state": "Active",
        "url": "https://dev.azure.com/org/project/_workitems/edit/12345"
      }
    }
  ],
  "totalCount": 2,
  "alreadyUserStoryCount": 1,
  "foundViaRecursionCount": 1,
  "notFoundCount": 0,
  "originalFetchFailedCount": 0
}
```

## 解析結果狀態說明

| 狀態 | 說明 | UserStory 欄位 |
|------|------|----------------|
| alreadyUserStoryOrAbove | 原始 Type 即為 User Story / Feature / Epic | 包含自身資訊 |
| foundViaRecursion | 透過遞迴 Parent 找到 User Story 以上類型 | 包含找到的 User Story 資訊 |
| notFound | 遞迴到頂層仍無法找到 User Story 以上類型 | null |
| originalFetchFailed | 原始 Work Item 在先前步驟就無法取得 | null |
