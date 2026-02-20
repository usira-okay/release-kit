# Quickstart: 整合 Release 資料

## 前置條件

1. Redis 伺服器已啟動（預設 `localhost:6379`）
2. 已執行過以下指令，確保 Redis 中有資料：
   - `fetch-gitlab-pr` 或 `fetch-bitbucket-pr`
   - `filter-gitlab-pr-by-user` 或 `filter-bitbucket-pr-by-user`
   - `fetch-azure-workitems`
   - `get-user-story`
3. `appsettings.json` 中已設定 `AzureDevOps:TeamMapping`

## 執行指令

```bash
dotnet run --project src/ReleaseKit.Console -- consolidate-release-data
```

## 預期結果

- 讀取 Redis Key `ReleaseKit:Bitbucket:PullRequests:ByUser` 與 `ReleaseKit:GitLab:PullRequests:ByUser`
- 讀取 Redis Key `ReleaseKit:AzureDevOps:WorkItems:UserStories`
- 整合後寫入 Redis Key `ReleaseKit:ConsolidatedReleaseData`
- stdout 輸出整合後的 JSON

## 輸出 JSON 結構範例

```json
{
  "projects": [
    {
      "projectName": "my-repo",
      "entries": [
        {
          "prTitle": "feature/VSTS12345-add-login",
          "workItemId": 12345,
          "teamDisplayName": "金流團隊",
          "authors": [
            { "authorName": "John Doe" }
          ],
          "pullRequests": [
            { "url": "https://gitlab.com/group/my-repo/merge_requests/1" }
          ],
          "originalData": {
            "workItem": { "..." },
            "pullRequests": [ { "..." } ]
          }
        }
      ]
    }
  ]
}
```

## 錯誤處理

| 情境 | 行為 |
|------|------|
| PR 資料 Key 不存在 | 拋出 InvalidOperationException，訊息指出缺少的 Key |
| Work Item 資料 Key 不存在 | 拋出 InvalidOperationException，訊息指出缺少的 Key |
| TeamMapping 找不到對應 | 使用原始 OriginalTeamName |

## 開發指令

```bash
# 建置
dotnet build src/release-kit.sln

# 執行測試
dotnet test tests/ReleaseKit.Application.Tests
```
