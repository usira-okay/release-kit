# Research: 整合 Release 資料

## R-001: PR 與 Work Item 配對機制

**Decision**: 使用 `UserStoryWorkItemOutput.PrId` 與 `MergeRequestOutput.PrId` 進行配對

**Rationale**:
- 現有資料模型中，`UserStoryWorkItemOutput.PrId` 記錄觸發 Work Item 查詢的 PR ID
- `MergeRequestOutput.PrId` 是平台內的 PR 唯一識別符
- 一個 Work Item 可能對應多個 PR（多對一關係），因此需以 Work Item 為主體，收集所有配對到的 PR

**Alternatives considered**:
- 使用 WorkItemId 配對：MergeRequestOutput.WorkItemId 是從 SourceBranch 解析的 VSTS ID，可作為反向查詢用，但主配對邏輯仍應以 PrId 為基準，因為 UserStoryWorkItemOutput 已明確記錄 PrId

## R-002: 資料整合流程設計

**Decision**: 採用以下流程

1. 從 Redis 讀取 PR 資料（Bitbucket ByUser + GitLab ByUser）→ 建立 `PrId → MergeRequestOutput` 查詢字典
2. 從 Redis 讀取 Work Item 資料（UserStories）
3. 建立 TeamMapping 查詢字典（忽略大小寫）
4. 遍歷 Work Items，依 PrId 配對 PR，合併相同 WorkItemId 的多筆記錄
5. 依 ProjectPath（split('/') 取最後一段）分組
6. 每組內依 TeamDisplayName 升冪 → WorkItemId 升冪 排序
7. 序列化後寫入 Redis

**Rationale**: 
- 此流程與現有 Task 模式一致（讀 Redis → 處理 → 寫 Redis）
- 以 Work Item 為主體整合，自然處理一對多的 PR 關聯

**Alternatives considered**:
- 以 PR 為主體整合：會導致同一 Work Item 重複出現在多筆記錄中，不符合需求「將配對到的資料放在一起」

## R-003: ProjectPath 分組策略

**Decision**: 從 PR 資料的 `ProjectResult.ProjectPath` 取 `split('/').Last()` 作為專案名稱

**Rationale**:
- PR 來源可能有多層路徑（如 `group/subgroup/project`）
- 需求明確要求 split('/') 後取最後一段
- 同一個 Work Item 的多個 PR 可能來自不同專案，因此 ProjectPath 應從 PR 所屬的 ProjectResult 中取得

**Alternatives considered**: 無

## R-004: TeamMapping 大小寫處理

**Decision**: 建立 `Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)` 做為 TeamMapping 查詢字典

**Rationale**:
- 需求明確要求 mapping 時忽略大小寫
- 使用 OrdinalIgnoreCase 效能最佳且符合需求
- 查詢不到時保留原始 OriginalTeamName

**Alternatives considered**:
- ToLower 轉換後比較：效能較差且有文化差異問題

## R-005: 整合結果 Redis Key 命名

**Decision**: `ConsolidatedReleaseData` → 完整 Key 為 `ReleaseKit:ConsolidatedReleaseData`（Instance Prefix 由 Redis 服務自動加上）

**Rationale**:
- 遵循現有命名慣例（PascalCase，以冒號分隔層級）
- 語義明確，表達「已整合的 Release 資料」

**Alternatives considered**:
- `AzureDevOps:WorkItems:Consolidated`：不準確，此資料不僅包含 Azure DevOps 資料
- `ReleaseNotes:Data`：太籠統

## R-006: Work Item PrId 為 null 的處理

**Decision**: PrId 為 null 的 Work Item 仍應出現在結果中，但 PR 資訊與作者資訊為空陣列，ProjectPath 使用 "unknown"

**Rationale**:
- spec 的 Edge Case 明確指出「該筆 Work Item 不會被配對到任何 PR，但仍應出現在整合結果中」
- 無 PrId 意味著無法得知 ProjectPath，使用 "unknown" 作為預設分組

**Alternatives considered**:
- 略過 PrId 為 null 的 Work Item：違反 spec 要求

## R-007: Console 指令名稱

**Decision**: `consolidate-release-data`

**Rationale**: 遵循現有 kebab-case 命名慣例，語義清晰表達「整合 Release 資料」

**Alternatives considered**: 
- `merge-release-data`：可能與 Git merge 概念混淆
- `aggregate-release-data`：過於技術化
