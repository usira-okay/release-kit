# GitLab PR 拉取功能使用指南

## 功能概述

此功能提供兩種方式拉取 GitLab 的 Merge Request (MR) 資訊：

1. **時間區間查詢**：拉取指定時間範圍內更新的 MR
2. **分支差異比較**：比較兩個分支的 commit 差異，並取得相關 MR

## 組態設定

### 1. appsettings.json 設定

在 `appsettings.json` 中加入 GitLab 組態：

```json
{
  "GitLab": {
    "Domain": "https://gitlab.com",
    "AccessToken": "your-personal-access-token-here",
    "DefaultProjectId": "your-org/your-project"
  }
}
```

### 2. 環境變數設定（推薦用於敏感資訊）

```bash
export GitLab__Domain="https://gitlab.com"
export GitLab__AccessToken="your-personal-access-token"
export GitLab__DefaultProjectId="your-org/your-project"
```

### 3. User Secrets 設定（開發環境推薦）

```bash
cd src/ReleaseKit.Console
dotnet user-secrets set "GitLab:AccessToken" "your-personal-access-token"
dotnet user-secrets set "GitLab:DefaultProjectId" "your-org/your-project"
```

## 取得 GitLab Access Token

1. 登入 GitLab
2. 前往 **User Settings** → **Access Tokens**
3. 建立新的 Personal Access Token
4. 選擇權限範圍：至少需要 `read_api` 權限
5. 複製產生的 Token

## 執行方式

### 方法一：使用 dotnet run

```bash
cd src/ReleaseKit.Console
dotnet run -- fetch-gitlab-pr
```

### 方法二：使用已建置的執行檔

```bash
cd src/ReleaseKit.Console/bin/Debug/net9.0
./ReleaseKit.Console fetch-gitlab-pr
```

## API 使用範例

### 程式化使用

```csharp
// 注入依賴
public class MyService
{
    private readonly IGitLabRepository _gitLabRepository;
    private readonly INow _now;

    public MyService(IGitLabRepository gitLabRepository, INow now)
    {
        _gitLabRepository = gitLabRepository;
        _now = now;
    }

    // 情境 1: 拉取最近 30 天的已合併 MR
    public async Task<IReadOnlyList<MergeRequest>> GetRecentMergedMRs()
    {
        var endTime = _now.UtcNow;
        var startTime = endTime.AddDays(-30);

        return await _gitLabRepository.FetchMergeRequestsByTimeRangeAsync(
            projectId: "my-org/my-project",
            startTime: startTime,
            endTime: endTime,
            state: "merged"
        );
    }

    // 情境 2: 拉取所有狀態的 MR（opened, merged, closed）
    public async Task<IReadOnlyList<MergeRequest>> GetAllMRs()
    {
        var endTime = _now.UtcNow;
        var startTime = endTime.AddDays(-30);

        return await _gitLabRepository.FetchMergeRequestsByTimeRangeAsync(
            projectId: "my-org/my-project",
            startTime: startTime,
            endTime: endTime,
            state: null // 不篩選狀態
        );
    }

    // 情境 3: 比較 develop 與 main 分支差異
    public async Task<IReadOnlyList<MergeRequest>> GetBranchDifferenceMRs()
    {
        return await _gitLabRepository.FetchMergeRequestsByBranchComparisonAsync(
            projectId: "my-org/my-project",
            sourceBranch: "develop",
            targetBranch: "main"
        );
    }
}
```

## 輸出資訊

每個 MergeRequest 包含以下資訊：

- **Id**: MR 的唯一識別碼
- **Number**: MR 的內部編號 (IID)
- **Title**: MR 標題
- **Description**: MR 描述
- **SourceBranch**: 來源分支
- **TargetBranch**: 目標分支
- **State**: MR 狀態（opened, merged, closed）
- **Author**: 作者使用者名稱
- **CreatedAt**: 建立時間
- **UpdatedAt**: 更新時間
- **MergedAt**: 合併時間（如已合併）
- **WebUrl**: MR 的網頁連結

## 常見問題

### 1. 401 Unauthorized 錯誤

**原因**：Access Token 無效或過期

**解決方法**：
- 檢查 Token 是否正確設定
- 確認 Token 權限是否包含 `read_api`
- 重新產生新的 Access Token

### 2. 404 Not Found 錯誤

**原因**：專案 ID 錯誤或無權限存取

**解決方法**：
- 確認專案 ID 格式正確（格式：`namespace/project`）
- 確認 Token 有權限存取該專案

### 3. 拉取資料時間過長

**原因**：MR 數量過多，API 分頁查詢時間長

**解決方法**：
- 縮小時間範圍
- 使用狀態篩選（如只拉取 `merged` 狀態）
- 考慮使用快取機制

### 4. Project ID 格式

GitLab 的 Project ID 有兩種格式：

1. **命名空間/專案名稱**：`namespace/project`（推薦）
   - 範例：`gitlab-org/gitlab`
   - 自動進行 URL 編碼

2. **數字 ID**：`12345678`
   - 範例：`42`
   - 可在專案設定頁面找到

## 效能優化建議

1. **使用適當的時間範圍**：避免查詢過長時間範圍
2. **狀態篩選**：如果只需要特定狀態的 MR，使用 `state` 參數
3. **快取結果**：對於不常變動的資料，考慮使用 Redis 快取
4. **分批處理**：大量資料建議分批查詢與處理

## 技術架構

```
Console Layer
    ↓ 使用
Application Layer (FetchGitLabPullRequestsTask)
    ↓ 使用
Domain Layer (IGitLabRepository)
    ↓ 實作
Infrastructure Layer (GitLabRepository)
    ↓ 呼叫
GitLab REST API v4
```

## 相關連結

- [GitLab API 文件](https://docs.gitlab.com/ee/api/)
- [Merge Requests API](https://docs.gitlab.com/ee/api/merge_requests.html)
- [Repository Comparison API](https://docs.gitlab.com/ee/api/repositories.html#compare-branches-tags-or-commits)
- [Personal Access Tokens](https://docs.gitlab.com/ee/user/profile/personal_access_tokens.html)
