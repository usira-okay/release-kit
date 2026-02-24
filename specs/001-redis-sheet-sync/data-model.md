# Data Model: Redis → Google Sheet 批次同步

**Feature**: 001-redis-sheet-sync
**Date**: 2026-02-24

## 既有實體（不修改）

### ConsolidatedReleaseResult

代表整合後的 Release 資料集合，以專案名稱為 key 分組。

| 欄位 | 類型 | 說明 |
|------|------|------|
| Projects | Dictionary\<string, List\<ConsolidatedReleaseEntry\>\> | 按專案名稱分組的資料 |

### ConsolidatedReleaseEntry

代表單筆整合的 Release 資料。

| 欄位 | 類型 | 說明 |
|------|------|------|
| Title | string | User Story 標題（可能為空） |
| WorkItemUrl | string | Work Item 連結（可能為空） |
| WorkItemId | int | Work Item ID |
| TeamDisplayName | string | 團隊顯示名稱 |
| Authors | List\<ConsolidatedAuthorInfo\> | 作者清單 |
| PullRequests | List\<ConsolidatedPrInfo\> | PR 清單 |
| OriginalData | ConsolidatedOriginalData | 原始資料（同步時不使用） |

### ConsolidatedAuthorInfo

| 欄位 | 類型 | 說明 |
|------|------|------|
| AuthorName | string | 作者名稱 |

### ConsolidatedPrInfo

| 欄位 | 類型 | 說明 |
|------|------|------|
| Url | string | PR 網址 |

## 新增概念模型（Application 層內部使用）

### SheetProjectSegment（值物件概念）

代表 Google Sheet 中一個專案區段的位置資訊。

| 欄位 | 類型 | 說明 |
|------|------|------|
| ProjectName | string | 專案名稱 |
| HeaderRowIndex | int | 專案表頭列的 0-based row index |
| DataStartRowIndex | int | 資料起始列的 0-based row index（HeaderRowIndex + 1） |
| DataEndRowIndex | int | 資料結束列的 0-based row index（下一個專案表頭列 - 1，或 Sheet 末尾） |

### SyncAction（列舉概念）

代表每筆資料的同步動作類型。

| 值 | 說明 |
|------|------|
| Insert | 新增（UniqueKey 不存在於 Sheet） |
| Update | 更新（UniqueKey 已存在於 Sheet） |

## 資料流

```text
Redis (ReleaseData:Consolidated)
    │
    ▼ HashGetAsync + JsonExtensions.ToTypedObject
    │
ConsolidatedReleaseResult
    │
    ▼ 遍歷 Projects Dictionary
    │
┌───────────────────────────────────────┐
│ 對每個 Project (key = projectName):   │
│                                       │
│  1. 計算 UniqueKey                    │
│  2. 比對 Sheet UniqueKeyColumn        │
│  3. 分類為 Insert 或 Update           │
└───────────────────────────────────────┘
    │
    ▼
┌───────────────────────────────────────┐
│ 批次操作：                             │
│  Step 1: 插入所有空白列                │
│  Step 2: 填入新增資料 + 更新既有資料    │
│  Step 3: 排序受影響的專案區段           │
└───────────────────────────────────────┘
    │
    ▼
Google Sheet (已更新)
```

## 欄位對應（由 ColumnMappingOptions 設定）

| 設定 Key | 對應 Sheet 欄位 | 資料來源 | 格式 |
|----------|----------------|---------|------|
| RepositoryNameColumn | 如 Z | projectName（比對用，不寫入） | 純文字 |
| FeatureColumn | 如 B | `VSTS{workItemId} - {title}` | HYPERLINK 公式 |
| TeamColumn | 如 D | teamDisplayName | 純文字 |
| AuthorsColumn | 如 E | authors[].authorName | 換行分隔，按 authorName 排序 |
| PullRequestUrlsColumn | 如 X | pullRequests[].url | 換行分隔，按 url 排序 |
| UniqueKeyColumn | 如 Y | `{workItemId}{projectName}` | 純文字 |
| AutoSyncColumn | 如 F | 固定值 `TRUE` | 純文字 |

## 排序規則

專案區段內資料列排序（僅排序 headerRow+1 到 nextHeaderRow-1 的範圍）：

| 優先序 | 欄位 | 方向 | 空白處理 |
|--------|------|------|---------|
| 1 | TeamColumn | 升序 | 排最後 |
| 2 | AuthorsColumn | 升序 | 排最後 |
| 3 | FeatureColumn | 升序 | 排最後 |
| 4 | UniqueKeyColumn | 升序 | 排最後 |
