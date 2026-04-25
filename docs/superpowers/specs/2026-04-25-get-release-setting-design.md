# Get Release Setting 功能設計規格

## 概述

新增 CLI 指令 `get-release-setting`，自動從 Redis 中讀取前置指令（`fetch-gitlab-release-branch` / `fetch-bitbucket-release-branch`）產生的 release branch 資訊，依據商業規則產生 GitLab 與 Bitbucket 的專案設定 JSON，並將結果寫入 Redis 以供後續 `fetch-*-pr` 指令使用。

## 問題描述

目前 release branch 的設定流程需要人工操作：
1. 執行 `fetch-gitlab-release-branch` / `fetch-bitbucket-release-branch` 取得各平台的 release branch 資訊
2. **人工判斷**每個專案的 FetchMode、SourceBranch、TargetBranch 等設定
3. 手動填入 `appsettings.json` 的 `GitLab.Projects` / `Bitbucket.Projects` 區塊

此流程繁瑣且容易出錯，需要一個自動化指令來取代步驟 2 和 3。

## 設計方案

### 方案選擇：單一 Task 處理全部

採用單一 `GetReleaseSettingTask` 處理 GitLab + Bitbucket 兩個平台的設定產生，遵循 KISS 與 YAGNI 原則。

### 資料流

```
Redis (GitLab:ReleaseBranches)  ──┐
                                  ├──> GetReleaseSettingTask ──> Redis (ReleaseSetting)
Redis (Bitbucket:ReleaseBranches) ┘         │
                                            └──> Console output (JSON)
```

### 輸入格式

前置指令寫入 Redis 的資料結構（Hash 結構）：

**Redis Key**: `GitLab`，Field: `ReleaseBranches`
```json
{
  "release/20250401": ["group/project-a", "group/project-b"],
  "release/20250315": ["group/project-c"],
  "NotFound": ["group/project-d"]
}
```

**Redis Key**: `Bitbucket`，Field: `ReleaseBranches`
```json
{
  "release/20250401": ["workspace/repo-a"],
  "NotFound": ["workspace/repo-b"]
}
```

### 輸出格式

**Redis Key**: `ReleaseSetting`（String 類型）

```json
{
  "gitLab": {
    "projects": [
      {
        "projectPath": "group/project-a",
        "targetBranch": "master",
        "fetchMode": "branchDiff",
        "sourceBranch": "release/20250401"
      },
      {
        "projectPath": "group/project-b",
        "targetBranch": "master",
        "fetchMode": "branchDiff",
        "sourceBranch": "release/20250401"
      },
      {
        "projectPath": "group/project-c",
        "targetBranch": "master",
        "fetchMode": "branchDiff",
        "sourceBranch": "release/20250315"
      },
      {
        "projectPath": "group/project-d",
        "targetBranch": "master",
        "fetchMode": "dateTimeRange",
        "sourceBranch": null,
        "startDateTime": null,
        "endDateTime": null
      }
    ]
  },
  "bitbucket": {
    "projects": [
      {
        "projectPath": "workspace/repo-a",
        "targetBranch": "develop",
        "fetchMode": "branchDiff",
        "sourceBranch": "release/20250401"
      },
      {
        "projectPath": "workspace/repo-b",
        "targetBranch": "develop",
        "fetchMode": "dateTimeRange",
        "sourceBranch": null,
        "startDateTime": null,
        "endDateTime": null
      }
    ]
  }
}
```

**無前置資料時**：
```json
{
  "gitLab": { "projects": [] },
  "bitbucket": { "projects": [] }
}
```

### FetchMode 判斷邏輯

對每個 `(branchName, projectPaths)` 配對，依優先序判斷：

1. **branchName == "NotFound"**
   → `FetchMode = DateTimeRange`, `SourceBranch = null`

2. **`ReleaseBranchHelper.IsReleaseBranch(branchName)` 回傳 false**（格式不符 `release/yyyyMMdd`）
   → `FetchMode = DateTimeRange`, `SourceBranch = null`

3. **`ReleaseBranchHelper.ParseReleaseBranchDate(branchName)` 解析出的日期與 `INow.UtcNow` 差距超過 3 個月**
   （即 release branch 日期 < `INow.UtcNow.AddMonths(-3)`）
   → `FetchMode = DateTimeRange`, `SourceBranch = null`

4. **其餘情況**
   → `FetchMode = BranchDiff`, `SourceBranch = branchName`

### 平台預設 TargetBranch

| 平台 | TargetBranch |
|------|-------------|
| GitLab | `master` |
| Bitbucket | `develop` |

### DateTimeRange 模式的時間參數

當 FetchMode 退回為 DateTimeRange 時，`StartDateTime` 和 `EndDateTime` 皆留 `null`，由全域設定或使用者手動填入。

## 架構設計

### 新增檔案

| 層級 | 檔案 | 說明 |
|------|------|------|
| Application | `Tasks/GetReleaseSettingTask.cs` | 主要 Task |
| Application | `Tasks/ReleaseSettingOutput.cs` | 頂層輸出 DTO |
| Application | `Tasks/PlatformSettingOutput.cs` | 平台層級 DTO |
| Application | `Tasks/ProjectSettingOutput.cs` | 專案層級 DTO |

### 修改檔案

| 層級 | 檔案 | 說明 |
|------|------|------|
| Application | `Tasks/TaskType.cs` | 新增 `GetReleaseSetting` 列舉值 |
| Application | `Tasks/TaskFactory.cs` | 新增 `GetReleaseSetting` case |
| Common | `Constants/RedisKeys.cs` | 新增 `ReleaseSetting` 常數 |
| Console | `Parsers/CommandLineParser.cs` | 新增 `get-release-setting` 指令對應 |
| Console | `Extensions/ServiceCollectionExtensions.cs` | 註冊 `GetReleaseSettingTask` |

### GetReleaseSettingTask 依賴

- `IRedisService`：讀取前置 release branch 資料 + 寫入產生的設定
- `INow`：取得當前 UTC 時間做 3 個月判斷

### 不需修改的部分

- **Domain 層**：不需要新增任何東西，`ReleaseBranchHelper` 已有 `IsReleaseBranch()` 和 `ParseReleaseBranchDate()` 足夠使用
- **Infrastructure 層**：不需要修改，Redis 讀寫已有完整介面
- **Configuration 類別**：不需要新增，輸出是 DTO 不是 Options

### DTO 結構

遵循憲法原則 XIII（單一類別檔案原則），每個 DTO 獨立檔案。

**ReleaseSettingOutput**：
```csharp
public record ReleaseSettingOutput
{
    public PlatformSettingOutput GitLab { get; init; } = new();
    public PlatformSettingOutput Bitbucket { get; init; } = new();
}
```

**PlatformSettingOutput**：
```csharp
public record PlatformSettingOutput
{
    public List<ProjectSettingOutput> Projects { get; init; } = new();
}
```

**ProjectSettingOutput**：
```csharp
public record ProjectSettingOutput
{
    public string ProjectPath { get; init; } = string.Empty;
    public string TargetBranch { get; init; } = string.Empty;
    public FetchMode FetchMode { get; init; }
    public string? SourceBranch { get; init; }
    public DateTimeOffset? StartDateTime { get; init; }
    public DateTimeOffset? EndDateTime { get; init; }
}
```

## 測試計畫

### 單元測試（`ReleaseKit.Application.Tests`）

| 測試場景 | 驗證目標 |
|----------|----------|
| 有 GitLab + Bitbucket release branch 資料 | 正確產生 BranchDiff 設定 |
| 只有 GitLab 資料，Bitbucket 無資料 | GitLab 有 Projects，Bitbucket 空陣列 |
| 兩個平台都無資料 | 兩個平台都是空 Projects |
| NotFound 專案 | FetchMode = DateTimeRange, SourceBranch = null |
| Branch 格式不符（如 `release/hotfix-123`） | FetchMode = DateTimeRange |
| Branch 日期超過 3 個月 | FetchMode = DateTimeRange |
| Branch 日期剛好 3 個月邊界 | 確認邊界行為 |
| GitLab TargetBranch 預設為 master | 驗證預設值 |
| Bitbucket TargetBranch 預設為 develop | 驗證預設值 |
| JSON 寫入 Redis 格式正確 | 驗證序列化結果 |
| 多個 branch 分組的專案 | 正確展開所有專案 |

### Mock 依賴

- `IRedisService`：控制讀取回傳值，驗證寫入呼叫
- `INow`：固定時間以確保 3 個月判斷可重現

## 設計決策記錄

1. **採用方案 A（單一 Task）**：符合 KISS/YAGNI 原則，避免過度抽象
2. **Redis 使用 String key**：一個 `ReleaseSetting` key 存整份 JSON（GitLab + Bitbucket 合併）
3. **DateTimeRange 時間留 null**：不自動計算，由全域設定或使用者手動填入
4. **NotFound 專案納入設定**：使用 DateTimeRange 模式，SourceBranch = null
5. **DTO 獨立檔案**：遵循憲法原則 XIII（單一類別檔案原則）
