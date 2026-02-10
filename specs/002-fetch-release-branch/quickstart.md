# Quickstart: 取得各 Repository 最新 Release Branch 名稱

**Feature Branch**: `002-fetch-release-branch`
**Date**: 2026-02-10

## 使用方式

### 取得 GitLab 各專案最新 Release Branch

```bash
dotnet run --project src/ReleaseKit.Console -- fetch-gitlab-release-branch
```

### 取得 Bitbucket 各專案最新 Release Branch

```bash
dotnet run --project src/ReleaseKit.Console -- fetch-bitbucket-release-branch
```

## 設定檔

使用現有 `appsettings.json` 中的 GitLab / Bitbucket 專案清單，**不需要額外設定**。

系統會讀取各平台 `Projects` 陣列中的 `ProjectPath`，對每個專案查詢以 `release/` 為前綴的分支。

```jsonc
{
  "GitLab": {
    "ApiUrl": "https://gitlab.com",
    "AccessToken": "your-token",
    "Projects": [
      { "ProjectPath": "group/backend-api" },
      { "ProjectPath": "group/frontend-app" }
    ]
  },
  "Bitbucket": {
    "ApiUrl": "https://api.bitbucket.org",
    "Email": "your-email",
    "AccessToken": "your-token",
    "Projects": [
      { "ProjectPath": "workspace/payment-service" }
    ]
  }
}
```

## 輸出範例

Console 會輸出 JSON 格式的查詢結果，同時存入 Redis。

```json
{
  "release/20260101": [
    "group/backend-api",
    "group/frontend-app"
  ],
  "release/20260106": [
    "workspace/payment-service"
  ],
  "NotFound": [
    "group/legacy-tool"
  ]
}
```

## Redis 儲存

| 平台 | Redis Key（含 InstanceName） |
|------|------------------------------|
| GitLab | `ReleaseKit:GitLab:ReleaseBranches` |
| Bitbucket | `ReleaseKit:Bitbucket:ReleaseBranches` |

## 判定邏輯

- 系統查詢每個專案所有以 `release/` 為前綴的分支
- 取字母排序最大的分支作為「最新 release branch」（如 `release/20260106` > `release/20260101`）
- 若專案無 release branch 或查詢失敗，歸類到 `"NotFound"` 群組
