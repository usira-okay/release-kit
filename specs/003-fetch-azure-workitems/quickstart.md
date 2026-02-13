# Quickstart: Azure DevOps Work Item 資訊擷取

**Feature**: 003-fetch-azure-workitems
**Date**: 2026-02-13

## 前置條件

1. Redis 中已存在過濾後的 PR 資料（至少一個 key 有值）：
   - `GitLab:PullRequests:ByUser`（由 `filter-gitlab-pr-by-user` 指令產出）
   - `Bitbucket:PullRequests:ByUser`（由 `filter-bitbucket-pr-by-user` 指令產出）
2. `appsettings.json` 中已設定 Azure DevOps 區段：
   - `AzureDevOps:OrganizationUrl`（如 `https://dev.azure.com/myorg`）
   - `AzureDevOps:PersonalAccessToken`（具備 Work Item 讀取權限的 PAT）

## 實作順序

建議依照以下順序實作，每個步驟完成後應可建置通過：

### Step 1: Domain Layer（基礎型別）

1. 新增 `WorkItem.cs`（Domain Entity）
2. 新增 `IAzureDevOpsRepository.cs`（Repository 介面）
3. 修改 `Error.cs`（新增 AzureDevOps 錯誤類別）

### Step 2: Common Layer（常數）

4. 修改 `RedisKeys.cs`（新增 `AzureDevOpsWorkItems`）
5. 修改 `HttpClientNames.cs`（新增 `AzureDevOps`）

### Step 3: Infrastructure Layer（API 通訊）

6. 新增 `AzureDevOpsWorkItemResponse.cs`（API Response Model）
7. 新增 `AzureDevOpsWorkItemMapper.cs`（Response → Entity Mapper）
8. 新增 `AzureDevOpsRepository.cs`（Repository 實作）

### Step 4: Application Layer（業務流程）

9. 新增 `WorkItemOutput.cs`（輸出 DTO）
10. 新增 `WorkItemFetchResult.cs`（彙整結果 DTO）
11. 修改 `FetchAzureDevOpsWorkItemsTask.cs`（完整實作）

### Step 5: Console Layer（DI 註冊）

12. 修改 `ServiceCollectionExtensions.cs`（HttpClient + Repository 註冊）

### Step 6: 測試

13. 新增 `AzureDevOpsRepositoryTests.cs`（Infrastructure 測試）
14. 新增 `FetchAzureDevOpsWorkItemsTaskTests.cs`（Application 測試）

## 關鍵實作模式

### VSTS ID 解析

```
正規表達式: VSTS(\d+)
匹配方式: Regex.Matches (找出所有匹配)
去重複: HashSet<int> 或 Distinct()
```

### HttpClient 認證設定

```
Named Client: HttpClientNames.AzureDevOps
Base Address: AzureDevOpsOptions.OrganizationUrl
Auth Header: Basic Auth (空 username + PAT)
```

### 錯誤處理流程

```
Result<WorkItem>.IsSuccess → WorkItemOutput(IsSuccess=true, 填入所有欄位)
Result<WorkItem>.IsFailure → WorkItemOutput(IsSuccess=false, 記錄 ErrorMessage)
```

## 驗證方式

完成實作後，執行以下驗證：

1. **建置**: `dotnet build src/release-kit.sln`
2. **單元測試**: `dotnet test` (確認所有測試通過)
3. **手動測試**（需 Redis + 有效 PAT）:
   ```
   dotnet run --project src/ReleaseKit.Console -- fetch-azure-workitems
   ```
