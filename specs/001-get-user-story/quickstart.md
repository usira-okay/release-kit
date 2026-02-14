# Quickstart: Get User Story

**Date**: 2026-02-14
**Branch**: `001-get-user-story`

## 前提條件

- .NET 9 SDK
- Redis 實例運行中
- Azure DevOps Personal Access Token 已設定於 `appsettings.json`
- 已執行 `fetch-gitlab-pr` 或 `fetch-bitbucket-pr` 抓取 PR 資料
- 已執行 `filter-gitlab-pr-by-user` 或 `filter-bitbucket-pr-by-user` 過濾使用者 PR

## 使用流程

### 1. 抓取 Work Item（既有指令，行為變更）

```bash
dotnet run --project src/ReleaseKit.Console -- fetch-azure-workitems
```

**變更點**: 輸出的每筆 Work Item 現在包含 PR 來源資訊（`sourcePullRequestId`、`sourceProjectName`、`sourcePRUrl`）。同一個 Work Item 出現在多筆 PR 中會產生多筆記錄。

### 2. 解析 User Story（新增指令）

```bash
dotnet run --project src/ReleaseKit.Console -- get-user-story
```

**行為**:
1. 從 Redis key `AzureDevOps:WorkItems` 讀取 Work Item 資料
2. 對每筆 Work Item：
   - 若類型為 User Story / Feature / Epic → 直接保留
   - 若類型為 Task / Bug / 其他 → 遞迴向上查詢 parent 直到找到 User Story / Feature / Epic
   - 若找不到 → 保留原始 Work Item 資料
   - 若原始抓取就失敗 → 保留失敗記錄
3. 結果寫入 Redis key `AzureDevOps:UserStories`

## 建置與測試

```bash
# 建置
dotnet build src/release-kit.sln

# 執行全部測試
dotnet test src/release-kit.sln

# 執行特定測試
dotnet test src/release-kit.sln --filter "FullyQualifiedName~GetUserStoryTaskTests"
dotnet test src/release-kit.sln --filter "FullyQualifiedName~AzureDevOpsWorkItemMapperTests"
```

## 可用的 Console 指令

| 指令 | 說明 |
|------|------|
| `fetch-gitlab-pr` | 抓取 GitLab Merge Request |
| `fetch-bitbucket-pr` | 抓取 Bitbucket Pull Request |
| `fetch-azure-workitems` | 抓取 Azure DevOps Work Item（含 PR 來源資訊） |
| `get-user-story` | **新增** 解析 Work Item 至 User Story 層級 |
| `update-googlesheet` | 更新 Google Sheets |
| `fetch-gitlab-release-branch` | 取得 GitLab Release Branch |
| `fetch-bitbucket-release-branch` | 取得 Bitbucket Release Branch |
| `filter-gitlab-pr-by-user` | 過濾 GitLab PR 依使用者 |
| `filter-bitbucket-pr-by-user` | 過濾 Bitbucket PR 依使用者 |

## Redis Keys

| Key | 說明 |
|-----|------|
| `AzureDevOps:WorkItems` | Work Item 抓取結果（含 PR 來源資訊） |
| `AzureDevOps:UserStories` | **新增** User Story 解析結果 |
