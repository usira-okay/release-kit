# Quick Start: Azure Work Item User Story Resolution

**Feature**: 將 Redis 中的 Azure Work Item 轉換為 User Story 層級

## 使用前提

1. 已執行 `fetch-azure-workitems` 命令，Redis 中存在 `AzureDevOps:WorkItems` 資料
2. Azure DevOps PAT Token 已設定於環境變數或 User Secrets
3. Redis 服務正常運行

## 基本使用

### 1. 執行 User Story 解析

```bash
cd src/ReleaseKit.Console
dotnet run -- get-user-story
```

**預期輸出**:
```
正在啟動 Release-Kit 應用程式...
正在從 Redis 讀取 Work Item 資料...
找到 100 筆 Work Item
開始解析 User Story...
  - 處理中: 10/100 (10%)
  - 處理中: 50/100 (50%)
  - 處理中: 100/100 (100%)
解析完成!
  - 原本就是 User Story: 30 筆
  - 透過遞迴找到: 60 筆
  - 無法找到: 8 筆
  - 取得失敗: 2 筆
已將結果儲存至 Redis (Key: AzureDevOps:WorkItems:UserStories)
```

### 2. 查看 Redis 結果

```bash
redis-cli GET AzureDevOps:WorkItems:UserStories
```

**範例輸出**:
```json
{
  "workItems": [
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
  ],
  "totalWorkItems": 100,
  "alreadyUserStoryCount": 30,
  "foundViaRecursionCount": 60,
  "notFoundCount": 8,
  "fetchFailedCount": 2
}
```

## 進階使用

### 自訂遞迴深度限制

編輯 `appsettings.json`：

```json
{
  "GetUserStory": {
    "MaxDepth": 15
  }
}
```

預設值為 10 層。

### 整合工作流程

```bash
# Step 1: 拉取 GitLab PR
dotnet run -- fetch-gitlab-pr

# Step 2: 拉取 Bitbucket PR
dotnet run -- fetch-bitbucket-pr

# Step 3: 從 PR 解析並拉取 Work Item
dotnet run -- fetch-azure-workitems

# Step 4: 將 Work Item 轉換為 User Story 層級
dotnet run -- get-user-story

# Step 5: 更新 Google Sheets
dotnet run -- update-googlesheet
```

## 資料結構說明

### resolutionStatus 欄位

| 值 | 說明 | 範例 |
|----|------|------|
| `alreadyUserStoryOrAbove` | 原本就是 User Story/Feature/Epic | 原始 Type 為 "User Story" |
| `foundViaRecursion` | 透過 Parent 查詢找到 | Bug → Task → User Story |
| `notFound` | 無法找到 User Story | Bug 沒有 Parent，或達到最大深度 |
| `originalFetchFailed` | 原始資料就取得失敗 | Work Item 404 Not Found |

### originalWorkItem 欄位

- 若 `resolutionStatus = alreadyUserStoryOrAbove`，此欄位為 `null`
- 若 `resolutionStatus = foundViaRecursion`，此欄位包含原始 Work Item 資訊
- 若 `resolutionStatus = notFound`，此欄位可能包含原始資訊（若原始資料可取得）
- 若 `resolutionStatus = originalFetchFailed`，此欄位為 `null`

## 常見問題

### Q: 如果 Redis 中沒有 Work Item 資料會怎樣？

**A**: 系統會顯示訊息 "Redis 中無 Work Item 資料" 並正常結束，不會拋出錯誤。

### Q: 如何處理循環參照？

**A**: 系統會自動偵測循環參照（如 A → B → A），並將 `resolutionStatus` 設為 `notFound`，錯誤訊息會註明 "偵測到循環參照"。

### Q: 遞迴深度超過限制會怎樣？

**A**: 系統會在達到最大深度時停止遞迴，將 `resolutionStatus` 設為 `notFound`，錯誤訊息會註明 "超過最大遞迴深度"。

### Q: 如果 Azure DevOps API 失敗會怎樣？

**A**: 系統會繼續處理其他 Work Item，將失敗的項目標記為 `isSuccess: false`，並在 `errorMessage` 記錄錯誤原因（如 "Work Item Not Found"、"Unauthorized" 等）。

### Q: 執行時間大約需要多久？

**A**: 
- 100 筆 Work Item，平均深度 2 層：約 10-20 秒
- 效能受 Azure DevOps API 回應時間影響
- 若遇到速率限制（200 requests/min），可能需要更長時間

### Q: 可以重複執行嗎？

**A**: 可以。每次執行會覆寫 `AzureDevOps:WorkItems:UserStories` 這個 Redis Key，不會影響原始的 `AzureDevOps:WorkItems` 資料。

## 錯誤訊息參考

| 錯誤訊息 | 原因 | 解決方式 |
|---------|------|---------|
| `Redis 連線失敗` | Redis 服務未啟動或連線字串錯誤 | 檢查 `appsettings.json` 中的 `Redis:ConnectionString` |
| `Azure DevOps Unauthorized` | PAT Token 過期或權限不足 | 更新 PAT Token，確保有 `Work Items (Read)` 權限 |
| `Work Item Not Found` | Work Item 已刪除或無權限存取 | 確認 Work Item ID 是否正確 |
| `偵測到循環參照` | Parent 關係形成循環 | 聯繫 Azure DevOps 管理員修正 Work Item 關係 |
| `超過最大遞迴深度` | Parent 鏈過長 | 增加 `appsettings.json` 中的 `GetUserStory:MaxDepth` 值 |

## 效能優化建議

### 減少 API 呼叫

若有大量 Work Item 需要處理，可考慮：
1. 先過濾 PR（使用 `filter-gitlab-pr-by-user` / `filter-bitbucket-pr-by-user`）
2. 只處理特定時間範圍的 Work Item

### 監控速率限制

Azure DevOps API 限制為 200 requests/minute，若遇到 429 錯誤：
1. 等待 1 分鐘後重試
2. 減少每批次處理的 Work Item 數量

## 整合範例

### PowerShell 腳本

```powershell
# release-notes.ps1
$ErrorActionPreference = "Stop"

Write-Host "開始產出 Release Notes..." -ForegroundColor Green

# 拉取所有 PR 與 Work Item
dotnet run --project src/ReleaseKit.Console -- fetch-gitlab-pr
dotnet run --project src/ReleaseKit.Console -- fetch-bitbucket-pr
dotnet run --project src/ReleaseKit.Console -- fetch-azure-workitems

# 轉換為 User Story 層級
dotnet run --project src/ReleaseKit.Console -- get-user-story

# 更新 Google Sheets
dotnet run --project src/ReleaseKit.Console -- update-googlesheet

Write-Host "Release Notes 產出完成!" -ForegroundColor Green
```

### Bash 腳本

```bash
#!/bin/bash
set -e

echo "開始產出 Release Notes..."

cd src/ReleaseKit.Console

# 拉取所有 PR 與 Work Item
dotnet run -- fetch-gitlab-pr
dotnet run -- fetch-bitbucket-pr
dotnet run -- fetch-azure-workitems

# 轉換為 User Story 層級
dotnet run -- get-user-story

# 更新 Google Sheets
dotnet run -- update-googlesheet

echo "Release Notes 產出完成!"
```

## 驗證結果

### 檢查統計數據

確認總數是否正確：
```
totalWorkItems = alreadyUserStoryCount + foundViaRecursionCount + notFoundCount + fetchFailedCount
```

### 抽樣檢查

隨機檢查幾筆 `foundViaRecursion` 的資料，確認：
1. `workItemId` 確實為 User Story 層級
2. `originalWorkItem` 包含原始 Work Item 資訊
3. Parent 關係正確（可到 Azure DevOps 驗證）

### 檢查失敗項目

查看 `isSuccess: false` 的項目：
1. 確認 `errorMessage` 是否合理
2. 若為 `notFound`，檢查是否真的沒有 Parent
3. 若為 `originalFetchFailed`，檢查 Work Item 是否已刪除
