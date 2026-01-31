# Implementation Plan: GitLab / Bitbucket PR 資訊擷取

**Branch**: `001-pr-info-fetch` | **Date**: 2026-01-31 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `specs/001-pr-info-fetch/spec.md`

---

## Summary

實作 GitLab 與 Bitbucket 平台的 PR/MR 資訊擷取功能，支援 DateTimeRange (時間區間) 與 BranchDiff (分支差異) 兩種模式。現有 TaskFactory 已有 GitLab 與 Bitbucket task stubs (目前拋出 `NotImplementedException`)，需完成實作。

---

## Technical Context

**Language/Version**: C# / .NET 9.0  
**Primary Dependencies**: HttpClient, System.Text.Json, Microsoft.Extensions.Options  
**Storage**: N/A (輸出 JSON 格式，可選擇性使用 Redis 快取)  
**Testing**: xUnit (現有測試框架)  
**Target Platform**: 現有 release-kit 專案 (Console Application)  
**Project Type**: Single (Clean Architecture - Domain/Application/Infrastructure/Console)  
**Performance Goals**: 能處理單次 1000+ PR 的擷取，30 秒內完成 100 筆 PR  
**Constraints**: 需遵循各平台 API Rate Limit，使用 Result Pattern 處理錯誤

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| 原則 | 狀態 | 說明 |
|------|------|------|
| I. TDD 測試驅動開發 | ✅ PASS | 將遵循 Red-Green-Refactor 循環 |
| II. DDD/CQRS | ✅ PASS | 使用現有 Domain/Application/Infrastructure 分層 |
| III. SOLID 原則 | ✅ PASS | 使用介面隔離 (ISourceControlRepository)，策略模式處理不同平台 |
| IV. KISS 簡單原則 | ✅ PASS | 重用現有 Task 架構，避免過度設計 |
| V. 結構化錯誤處理 | ✅ PASS | 將實作 Result Pattern，禁止 try-catch |
| VI. 效能與快取 | ✅ PASS | 可選擇性使用現有 Redis 快取 |
| VII. 避免硬編碼 | ✅ PASS | 使用現有 Options 配置模式 |
| VIII. 文件與註解 | ✅ PASS | 使用繁體中文 XML 註解 |
| IX. JSON 序列化 | ✅ PASS | 使用 System.Text.Json |
| X. 程式進入點 | ✅ PASS | 不修改 Program.cs 業務邏輯 |
| XI. 檔案組織 | ✅ PASS | 一個類別一個檔案 |

---

## API Specifications

### GitLab API

#### 取得 Merge Requests (DateTimeRange 模式)

```http
GET /projects/:id/merge_requests
```

**Authentication**: `PRIVATE-TOKEN: <token>` 或 `Authorization: Bearer <token>`

**Query Parameters**:

| 參數 | 必填 | 說明 |
|------|------|------|
| `state` | ✅ | 設為 `merged` |
| `target_branch` | ✅ | 目標分支名稱 |
| `updated_after` | ✅ | ISO 8601 格式 |
| `updated_before` | ✅ | ISO 8601 格式 |
| `scope` | 建議 | 設為 `all` 取得所有 MR |
| `per_page` | 建議 | 分頁筆數 (預設 20，最大 100) |

**Response Field Mapping**:

| API 欄位 | 輸出欄位 | 型別 |
|----------|----------|------|
| `title` | `Title` | string |
| `description` | `Description` | string |
| `source_branch` | `SourceBranch` | string |
| `target_branch` | `TargetBranch` | string |
| `created_at` | `CreatedAt` | DateTimeOffset |
| `merged_at` | `MergedAt` | DateTimeOffset |
| `state` | `State` | string |
| `author.id` | `AuthorUserId` | string |
| `author.username` | `AuthorName` | string |
| `web_url` | `PRUrl` | string |

**Note**: GitLab API 僅支援 `updated_after`/`updated_before` 篩選，需在程式端以 `merged_at` 二次過濾。

---

#### 比較分支差異 (BranchDiff 模式)

```http
GET /projects/:id/repository/compare
```

| 參數 | 必填 | 說明 |
|------|------|------|
| `from` | ✅ | 來源分支 |
| `to` | ✅ | 目標分支 |
| `straight` | 建議 | 設為 `false` |

#### 取得 Commit 關聯的 MR

```http
GET /projects/:id/repository/commits/:sha/merge_requests
```

---

### Bitbucket API

#### 取得 Pull Requests (DateTimeRange 模式)

```http
GET /2.0/repositories/{workspace}/{repo_slug}/pullrequests
```

**Authentication**: `Authorization: Bearer <token>`

**Query Parameters**:

| 參數 | 必填 | 說明 |
|------|------|------|
| `state` | ✅ | 設為 `MERGED` |
| `q` | 建議 | 查詢條件 |
| `fields` | ✅ | 設為 `*.*` 取得完整欄位 |
| `pagelen` | 建議 | 分頁筆數 (預設 10，最大 50) |

**Response Field Mapping**:

| API 欄位 | 輸出欄位 | 型別 |
|----------|----------|------|
| `title` | `Title` | string |
| `summary.raw` | `Description` | string |
| `source.branch.name` | `SourceBranch` | string |
| `destination.branch.name` | `TargetBranch` | string |
| `created_on` | `CreatedAt` | DateTimeOffset |
| `closed_on` | `MergedAt` | DateTimeOffset |
| `state` | `State` | string |
| `author.uuid` | `AuthorUserId` | string |
| `author.display_name` | `AuthorName` | string |
| `links.html.href` | `PRUrl` | string |

**Note**: Bitbucket API 僅支援 `updated_on` 篩選，需加上 `fields=*.*` 取得 `closed_on` 欄位。

---

#### 比較分支差異 (BranchDiff 模式)

```http
GET /2.0/repositories/{workspace}/{repo_slug}/diffstat/{spec}
```

**Path**: `spec` 格式為 `{from}..{to}`

#### 取得 Commit 關聯的 PR

```http
GET /2.0/repositories/{workspace}/{repo_slug}/commit/{commit}/pullrequests
```

---

## Processing Logic

### DateTimeRange Mode Flow

```
[開始]
    ↓
[讀取設定: TargetBranch, StartDateTime, EndDateTime]
    ↓
[平台判斷] ─── GitLab ───→ [呼叫 MR API with updated_after/updated_before]
    │                              ↓
    │                       [處理分頁取得所有資料]
    │                              ↓
    │                       [程式端過濾 merged_at 時間範圍]
    │                              ↓
    └── Bitbucket ───→ [呼叫 PR API with state=MERGED & fields=*.*]
                               ↓
                        [處理分頁取得所有資料]
                               ↓
                        [程式端過濾 closed_on 時間範圍]
                               ↓
                        [映射輸出結構]
                               ↓
                           [結束]
```

### BranchDiff Mode Flow

```
[開始]
    ↓
[讀取設定: SourceBranch, TargetBranch]
    ↓
[取得所有 release/yyyyMMdd 分支]
    ↓
[依日期由舊到新排序]
    ↓
[判斷 SourceBranch 是否為最新]
    │
    ├── 是 (最新) ──→ [比較對象 = TargetBranch]
    │
    └── 否 ──→ [比較對象 = 下一版 release 分支]
          ↓
[呼叫 Compare API 取得 commits 差異]
    ↓
[對每個 commit 呼叫 API 取得關聯的 PR]
    ↓
[去重複並映射輸出結構]
    ↓
[結束]
```

---

## Project Structure

### Documentation (this feature)

```text
specs/001-pr-info-fetch/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (API contracts)
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── ReleaseKit.Domain/
│   ├── Abstractions/
│   │   ├── ITask.cs                          # 已存在
│   │   └── ISourceControlRepository.cs       # 新增：Source Control 抽象
│   ├── Entities/
│   │   └── MergeRequest.cs                   # 新增：MR 實體
│   ├── ValueObjects/
│   │   └── SourceControlPlatform.cs          # 新增：平台類型
│   └── Common/
│       ├── Result.cs                         # 新增：Result Pattern
│       └── Error.cs                          # 新增：錯誤定義
│
├── ReleaseKit.Application/
│   └── Tasks/
│       ├── FetchGitLabPullRequestsTask.cs    # 修改：實作邏輯
│       └── FetchBitbucketPullRequestsTask.cs # 修改：實作邏輯
│
├── ReleaseKit.Infrastructure/
│   ├── SourceControl/
│   │   ├── GitLab/
│   │   │   ├── GitLabRepository.cs           # 新增：GitLab 實作
│   │   │   ├── GitLabMergeRequestMapper.cs   # 新增：欄位映射
│   │   │   └── Models/                       # 新增：API 回應模型
│   │   │       ├── GitLabMergeRequestResponse.cs
│   │   │       └── GitLabCompareResponse.cs
│   │   └── Bitbucket/
│   │       ├── BitbucketRepository.cs        # 新增：Bitbucket 實作
│   │       ├── BitbucketPullRequestMapper.cs # 新增：欄位映射
│   │       └── Models/                       # 新增：API 回應模型
│   │           ├── BitbucketPullRequestResponse.cs
│   │           └── BitbucketDiffstatResponse.cs
│   └── Configuration/
│       ├── GitLabOptions.cs                  # 已存在
│       ├── BitbucketOptions.cs               # 已存在
│       └── FetchModeOptions.cs               # 已存在（需補 TargetBranch）
│
└── ReleaseKit.Console/
    └── Extensions/
        └── ServiceCollectionExtensions.cs    # 修改：註冊新服務

tests/
├── ReleaseKit.Domain.Tests/
│   └── Entities/
│       └── MergeRequestTests.cs              # 新增
├── ReleaseKit.Application.Tests/
│   └── Tasks/
│       ├── FetchGitLabPullRequestsTaskTests.cs    # 修改
│       └── FetchBitbucketPullRequestsTaskTests.cs # 修改
└── ReleaseKit.Infrastructure.Tests/
    └── SourceControl/
        ├── GitLab/
        │   └── GitLabRepositoryTests.cs      # 新增
        └── Bitbucket/
            └── BitbucketRepositoryTests.cs   # 新增
```

**Structure Decision**: 使用現有 Clean Architecture 分層結構，新增程式碼放置於對應層級的 SourceControl 目錄下。

---

## Pagination Handling

| 平台 | 分頁參數 | 說明 |
|------|----------|------|
| GitLab | `page`, `per_page` | 頁碼式分頁，預設 20 筆，最大 100 筆 |
| Bitbucket | `page`, `pagelen`, `next` | 使用 response 中的 `next` 連結取得下一頁 |

---

## Authentication

| 平台 | 驗證方式 | Header 格式 |
|------|----------|-------------|
| GitLab | Private Token | `PRIVATE-TOKEN: <token>` |
| GitLab | OAuth 2.0 | `Authorization: Bearer <token>` |
| Bitbucket | Access Token | `Authorization: Bearer <token>` |

---

## Complexity Tracking

> 無違反 Constitution 的複雜度需要追蹤

---

## Constitution Check (Post-Design)

*Re-evaluated after Phase 1 design completion.*

| 原則 | 狀態 | 設計驗證 |
|------|------|----------|
| I. TDD 測試驅動開發 | ✅ PASS | 測試結構已規劃於 tests/ 目錄 |
| II. DDD/CQRS | ✅ PASS | MergeRequest 實體放置於 Domain 層，Repository 介面抽象化 |
| III. SOLID 原則 | ✅ PASS | ISourceControlRepository 介面隔離，GitLab/Bitbucket 各自實作 |
| IV. KISS 簡單原則 | ✅ PASS | 重用現有 Task/Options 架構，無過度抽象 |
| V. 結構化錯誤處理 | ✅ PASS | Result<T> 與 Error 類型已定義於 research.md |
| VI. 效能與快取 | ✅ PASS | 分頁處理策略已規劃，可選擇性使用 Redis |
| VII. 避免硬編碼 | ✅ PASS | 所有設定透過 Options 配置 |
| VIII. 文件與註解 | ✅ PASS | API 契約使用繁體中文 XML 註解 |
| IX. JSON 序列化 | ✅ PASS | 使用 System.Text.Json with JsonPropertyName |
| X. 程式進入點 | ✅ PASS | 僅修改 ServiceCollectionExtensions 註冊服務 |
| XI. 檔案組織 | ✅ PASS | 每個類別獨立檔案，已規劃於 Project Structure |

**總結**: 所有 Constitution 原則皆通過驗證，設計符合規範。

---

## Reference Documentation

- [GitLab Merge Requests API](https://docs.gitlab.com/api/merge_requests/)
- [GitLab Repositories API](https://docs.gitlab.com/api/repositories/)
- [Bitbucket Pullrequests API](https://developer.atlassian.com/cloud/bitbucket/rest/api-group-pullrequests/)
- [Bitbucket Commits API](https://developer.atlassian.com/cloud/bitbucket/rest/api-group-commits/)
