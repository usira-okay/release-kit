# Implementation Plan: PR 與 Work Item 關聯追蹤改善

**Branch**: `001-workitem-pr-tracking` | **Date**: 2026-02-19 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-workitem-pr-tracking/spec.md`

## Summary

移除 `FetchAzureDevOpsWorkItemsTask` 中對 Work Item ID 的去重複邏輯，保留每個 PR 與 Work Item 的對應關係，並在 `WorkItemOutput` 中新增 `PrUrl` 欄位記錄觸發此次查詢的 PR 來源；`GetUserStoryTask` 將 `PrUrl` 傳遞至 `UserStoryWorkItemOutput`，同時確保 `OriginalWorkItem` 中不含 `PrUrl`（使用 C# record with-expression 清除）。

## Technical Context

**Language/Version**: C# / .NET 8
**Primary Dependencies**: MediatR（CQRS）、StackExchange.Redis、Microsoft.Extensions.Logging
**Storage**: Redis（透過 `IRedisService` 抽象層）
**Testing**: xUnit
**Target Platform**: Linux Server
**Project Type**: 背景任務排程服務（Console/Worker Service）
**Performance Goals**: 移除去重複後，同一 Work Item ID 若出現在 N 個 PR 中，將發出 N 次 Azure DevOps API 請求（預期量仍在可接受範圍內）
**Constraints**: 僅修改 Application 層，不涉及 Infrastructure 或 Domain 層
**Scale/Scope**: 影響 5 個 Application 層檔案，無 API 路徑變更

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| 原則 | 狀態 | 說明 |
|------|------|------|
| I. TDD | ✅ | 所有修改需先撰寫測試再實作 |
| II. DDD / CQRS | ✅ | 僅修改 Application 層的 Task（Command Handler 概念），不破壞領域邊界 |
| III. SOLID | ✅ | 各類別職責不變，僅擴充欄位與調整內部邏輯 |
| IV. KISS | ✅ | 使用 C# record with-expression 清除欄位，簡單直觀；無新增抽象層 |
| V. 錯誤處理 | ✅ | 不新增 try-catch，維持現有模式 |
| VI. 效能與快取優先 | ✅ | 繼續透過 IRedisService 進行快取存取 |
| VII. 避免硬編碼 | ✅ | 無新增魔術數字或字串常數 |
| VIII. 文件與註解 | ✅ | 所有新增欄位需附 XML Summary 繁體中文註解 |
| IX. JSON 序列化 | ✅ | 新增欄位自動納入現有 JsonExtensions 序列化流程 |
| X. Program.cs 整潔 | ✅ | 無影響 |
| XI. 一類別一檔案 | ✅ | 無新增類別 |
| XII. RESTful API | ✅ | 無新增 API 路徑 |
| XIII. 組態管理 | ✅ | 無新增設定值 |
| XIV. 程式碼重用 | ✅ | 優先重用現有 WorkItemOutput、with-expression 等模式 |

**Constitution Check 結果**: 全部通過，無違規，無需 Complexity Tracking。

## Project Structure

### Documentation (this feature)

```text
specs/001-workitem-pr-tracking/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Phase 2 output (/speckit.tasks - NOT created by /speckit.plan)
```

### Source Code (repository root)

本功能**不新增任何新檔案**，僅修改 Application 層現有檔案：

```text
src/
└── ReleaseKit.Application/
    ├── Common/
    │   ├── WorkItemOutput.cs           ← 新增 PrUrl 欄位
    │   ├── UserStoryWorkItemOutput.cs  ← 新增 PrUrl 欄位
    │   └── WorkItemFetchResult.cs      ← 更新 XML 註解
    └── Tasks/
        ├── FetchAzureDevOpsWorkItemsTask.cs  ← 移除去重複，填入 PrUrl
        └── GetUserStoryTask.cs               ← 傳遞 PrUrl，清除 OriginalWorkItem.PrUrl

tests/
└── ReleaseKit.Application.Tests/
    └── Tasks/
        ├── FetchAzureDevOpsWorkItemsTaskTests.cs  ← 更新/新增測試
        └── GetUserStoryTaskTests.cs               ← 更新/新增測試
```

**Structure Decision**: 單一專案架構，Application 層直接修改，無需新建資料夾或檔案。

## Reusable Components Identified

| 元件 | 位置 | 重用方式 |
|------|------|---------|
| `WorkItemOutput` record | `WorkItemOutput.cs` | 擴充新增 `PrUrl` 欄位，不重新設計 |
| `UserStoryWorkItemOutput` record | `UserStoryWorkItemOutput.cs` | 擴充新增 `PrUrl` 欄位，不重新設計 |
| C# record with-expression | 語言特性 | 用於清除 `OriginalWorkItem.PrUrl` |
| 現有 Redis 存取模式 | `IRedisService` | 無需改動 |
| 現有 `WorkItemFetchResult` 結構 | `WorkItemFetchResult.cs` | 僅更新 XML 註解，不改結構 |

## Detailed Design

### 設計決策 1：ExtractWorkItemIdsFromPRs 返回型別

```
舊: HashSet<int>  → 去重複，丟失 PR 來源
新: List<(string prUrl, int workItemId)>  → 保留重複，攜帶 PR 來源
```

### 設計決策 2：FetchWorkItemsAsync 簽章

```
舊: Task<List<WorkItemOutput>> FetchWorkItemsAsync(HashSet<int> workItemIds)
新: Task<List<WorkItemOutput>> FetchWorkItemsAsync(IReadOnlyList<(string prUrl, int workItemId)> workItemPairs)
```

### 設計決策 3：GetUserStoryTask PrUrl 傳遞

在所有建立 `UserStoryWorkItemOutput` 的地方（ProcessWorkItemAsync 中共 4 處）新增 `PrUrl = workItem.PrUrl`。

### 設計決策 4：OriginalWorkItem 清除 PrUrl

```csharp
// 不直接傳入 workItem，而是用 with-expression 清除 PrUrl
OriginalWorkItem = workItem with { PrUrl = null }
```

此改法僅適用於有 `OriginalWorkItem = workItem` 的情況（行 160、181、203、220）。

### 設計決策 5：Log 訊息更新

行 62 的 log 訊息需從：
```
"從 {PRCount} 個 PR 中解析出 {WorkItemCount} 個不重複的 Work Item ID"
```
改為：
```
"從 {PRCount} 個 PR 中解析出 {WorkItemCount} 個 Work Item ID（含重複）"
```

## Phase 1 Re-check: Constitution Check

設計完成後重新驗證，確認：
- 所有修改僅在 Application 層，無架構層級違規
- 使用 with-expression 屬 KISS 原則實踐
- 新增欄位不影響現有 JSON 序列化契約（Redis 中舊資料無 `PrUrl` 欄位，反序列化時為 null，向下相容）
- **全部通過，無新增複雜度違規**
