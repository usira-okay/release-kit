# Tasks: PR 與 Work Item 關聯追蹤改善

**Input**: Design documents from `/specs/001-workitem-pr-tracking/`
**Prerequisites**: plan.md ✅ | spec.md ✅ | research.md ✅ | data-model.md ✅

**Tests**: 依據專案 Constitution（TDD 為強制原則），每個 User Story 的實作任務前必須先撰寫失敗測試。

**Organization**: 任務依 User Story 分組，各 Story 可獨立實作與驗證。

## Format: `[ID] [P?] [Story] Description`

- **[P]**: 可平行執行（不同檔案，無相依未完成任務）
- **[Story]**: 對應 User Story（US1、US2、US3）
- 路徑基於 `src/ReleaseKit.Application/`

---

## Phase 1: Foundational（資料模型基礎）

**Purpose**: 新增 `PrUrl` 欄位至共用 DTO，此為 US2 與 US3 的必要前提

**⚠️ CRITICAL**: US2 與 US3 依賴此階段完成

- [ ] T001 [P] 在 `src/ReleaseKit.Application/Common/WorkItemOutput.cs` 新增 `string? PrUrl` 欄位，附繁體中文 XML Summary 註解
- [ ] T002 [P] 在 `src/ReleaseKit.Application/Common/UserStoryWorkItemOutput.cs` 新增 `string? PrUrl` 欄位，附繁體中文 XML Summary 註解，並說明不寫入 OriginalWorkItem 的原因
- [ ] T003 更新 `src/ReleaseKit.Application/Common/WorkItemFetchResult.cs` 中 `TotalWorkItemsFound` 欄位的 XML 註解，移除「不重複」描述，改為說明此為包含重複的 Work Item ID 總數

**Checkpoint**: 建置狀態 ✅ 可建置 | 測試狀態 ✅ 通過（無新測試，僅欄位擴充）

---

## Phase 2: User Story 1 - 保留 PR 對應的重複 Work Item ID（Priority: P1）🎯 MVP

**Goal**: 移除 `ExtractWorkItemIdsFromPRs` 中的 `.ToHashSet()` 去重複邏輯，改為保留每個 PR 對應的 (prUrl, workItemId) 對

**Independent Test**: 建立包含重複 WorkItemId 的 `MergeRequestOutput` 清單，執行 `ExtractWorkItemIdsFromPRs`，驗證結果清單長度與輸入相同（不去重複）

### 測試（TDD - 先寫測試，確認失敗後再實作）

> **⚠️ TDD 規則：執行測試確認 RED 後，再進行 Implementation**

- [ ] T004 [US1] 在 `tests/ReleaseKit.Application.Tests/Tasks/FetchAzureDevOpsWorkItemsTaskTests.cs` 新增測試：當多個 PR 指向同一 WorkItemId 時，`ExtractWorkItemIdsFromPRs` 結果包含重複項目（驗證不去重複）

### 實作

- [ ] T005 [US1] 修改 `src/ReleaseKit.Application/Tasks/FetchAzureDevOpsWorkItemsTask.cs` 的 `ExtractWorkItemIdsFromPRs` 方法：返回型別從 `HashSet<int>` 改為 `List<(string prUrl, int workItemId)>`，以 `.Select(pr => (pr.PRUrl, pr.WorkItemId!.Value)).ToList()` 取代 `.ToHashSet()`；同時更新方法 XML 註解移除「不重複」說明
- [ ] T006 [US1] 在 `src/ReleaseKit.Application/Tasks/FetchAzureDevOpsWorkItemsTask.cs` 更新 `FetchWorkItemsAsync` 方法簽章：參數型別從 `HashSet<int>` 改為 `IReadOnlyList<(string prUrl, int workItemId)>`，並更新 `ExecuteAsync` 中的呼叫端與相關變數

**Checkpoint**: 建置狀態 ✅ 可建置 | 測試狀態 ✅ 通過（T004 通過、既有測試通過）

---

## Phase 3: User Story 2 - 將 PR ID 記錄至 Work Item 物件並儲存（Priority: P2）

**Goal**: 在 `FetchWorkItemsAsync` 建立 `WorkItemOutput` 時，將觸發查詢的 `prUrl` 寫入 `PrUrl` 欄位，使 Redis 中的 Work Item 資料包含 PR 來源

**Independent Test**: 執行 `FetchAzureDevOpsWorkItemsTask` 後，查詢 Redis 中的 `WorkItemOutput`，驗證 `PrUrl` 欄位包含對應 PR 的 `PRUrl` 值

### 測試（TDD - 先寫測試，確認失敗後再實作）

> **⚠️ TDD 規則：執行測試確認 RED 後，再進行 Implementation**

- [ ] T007 [US2] 在 `tests/ReleaseKit.Application.Tests/Tasks/FetchAzureDevOpsWorkItemsTaskTests.cs` 新增測試：`FetchWorkItemsAsync` 建立的 `WorkItemOutput` 中 `PrUrl` 欄位值等於輸入 PR 的 `PRUrl`（同時驗證成功查詢與失敗查詢兩種情境）

### 實作

- [ ] T008 [US2] 在 `src/ReleaseKit.Application/Tasks/FetchAzureDevOpsWorkItemsTask.cs` 的 `FetchWorkItemsAsync` 方法中，更新迴圈以使用 `(string prUrl, int workItemId)` 解構，並在兩個 `WorkItemOutput` 建立位置（成功行 162 與失敗行 177）新增 `PrUrl = prUrl`
- [ ] T009 [US2] 在 `src/ReleaseKit.Application/Tasks/FetchAzureDevOpsWorkItemsTask.cs` 的 `ExecuteAsync` 方法中更新 Log 訊息（行 62）：從「解析出 {WorkItemCount} 個不重複的 Work Item ID」改為「解析出 {WorkItemCount} 個 Work Item ID（含重複）」

**Checkpoint**: 建置狀態 ✅ 可建置 | 測試狀態 ✅ 通過（T007 通過、既有測試通過）

---

## Phase 4: User Story 3 - User Story 層級僅在 User Story 物件記錄 PR ID（Priority: P3）

**Goal**: `GetUserStoryTask.ProcessWorkItemAsync` 在建立 `UserStoryWorkItemOutput` 時，從 `workItem.PrUrl` 取得並設定 `PrUrl`；在設定 `OriginalWorkItem` 時，使用 `workItem with { PrUrl = null }` 確保 `OriginalWorkItem` 不含 PR 識別資訊

**Independent Test**: 執行 `GetUserStoryTask` 後，驗證 `UserStoryWorkItemOutput.PrUrl` 不為 null，且 `UserStoryWorkItemOutput.OriginalWorkItem.PrUrl` 為 null

### 測試（TDD - 先寫測試，確認失敗後再實作）

> **⚠️ TDD 規則：執行測試確認 RED 後，再進行 Implementation**

- [ ] T010 [US3] 在 `tests/ReleaseKit.Application.Tests/Tasks/GetUserStoryTaskTests.cs` 新增測試：`ProcessWorkItemAsync` 結果中 `UserStoryWorkItemOutput.PrUrl` 等於輸入 `WorkItemOutput.PrUrl`，且 `OriginalWorkItem.PrUrl` 為 null（涵蓋 FoundViaRecursion 與 NotFound 兩種情境）

### 實作

- [ ] T011 [US3] 在 `src/ReleaseKit.Application/Tasks/GetUserStoryTask.cs` 的 `ProcessWorkItemAsync` 方法中，於所有 `UserStoryWorkItemOutput` 建立位置新增 `PrUrl = workItem.PrUrl`（共 4 個建立點：OriginalFetchFailed、AlreadyUserStoryOrAbove、NotFound×2、FoundViaRecursion）
- [ ] T012 [US3] 在 `src/ReleaseKit.Application/Tasks/GetUserStoryTask.cs` 的 `ProcessWorkItemAsync` 方法中，將所有 `OriginalWorkItem = workItem` 替換為 `OriginalWorkItem = workItem with { PrUrl = null }`（共 4 個位置：行 160、181、203、220）

**Checkpoint**: 建置狀態 ✅ 可建置 | 測試狀態 ✅ 通過（T010 通過、既有測試通過）

---

## Phase 5: Polish & 驗證

**Purpose**: 最終完整驗證，確保所有 User Story 均可正常運作

- [ ] T013 執行完整建置確認無編譯錯誤：`dotnet build`
- [ ] T014 執行所有單元測試確認全部通過：`dotnet test`

**Final Checkpoint**: 建置狀態 ✅ 可建置 | 測試狀態 ✅ 通過（全部測試）

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 1)**: 無相依，立即可開始（T001、T002 可平行執行）
- **US1 (Phase 2)**: 不依賴 Phase 1（不需要 PrUrl 欄位）→ 可與 Phase 1 平行
- **US2 (Phase 3)**: 依賴 Phase 1（需 `WorkItemOutput.PrUrl`）+ Phase 2（需 `(prUrl, workItemId)` 返回型別）
- **US3 (Phase 4)**: 依賴 Phase 1（需 `UserStoryWorkItemOutput.PrUrl`）+ Phase 2 + Phase 3（需 `WorkItemOutput.PrUrl` 有值）
- **Polish (Phase 5)**: 依賴所有 Phase 完成

### User Story Dependencies

```
Phase 1 (T001, T002, T003)
    │
    ├──── Phase 2 US1 (T004→T005→T006)
    │         │
    │         └──── Phase 3 US2 (T007→T008→T009)
    │                   │
    └─────────────────── Phase 4 US3 (T010→T011→T012)
                              │
                          Phase 5 Polish (T013, T014)
```

### Parallel Opportunities

- T001 與 T002 可平行執行（不同檔案）
- Phase 1 (T001、T002) 與 Phase 2 前半段 (T004) 可平行開始
- T011 與 T012 作業於同一個方法，需循序執行

---

## Parallel Example

```bash
# Phase 1 平行執行：
Task A: "T001 - 新增 PrUrl 至 WorkItemOutput.cs"
Task B: "T002 - 新增 PrUrl 至 UserStoryWorkItemOutput.cs"
# T003 可在 A、B 完成後立即執行（不同檔案，亦可平行）
Task C: "T003 - 更新 WorkItemFetchResult.cs XML 註解"
```

---

## Implementation Strategy

### MVP First（僅 User Story 1）

1. 完成 Phase 2 US1（T004 → T005 → T006）
2. **STOP & VALIDATE**：確認重複 Work Item ID 被保留
3. 驗證通過後繼續

### Incremental Delivery

1. Phase 1 Foundational → 模型就緒
2. Phase 2 US1 → 去重複邏輯移除（MVP）
3. Phase 3 US2 → PR ID 記錄至 Work Item
4. Phase 4 US3 → User Story 層級 PrUrl 追蹤
5. Phase 5 Polish → 最終驗證

---

## Notes

- TDD 是**強制原則**（Constitution I）：每個 User Story 的測試任務必須在實作前完成，並確認測試為 **RED** 狀態後，再進行實作
- [P] 任務 = 不同檔案，無未完成的相依任務
- [Story] 標籤對應 spec.md 中的 User Story，用於追蹤
- Foundational 階段的 DTO 變更向下相容（`PrUrl` 為 nullable，Redis 中舊資料反序列化時為 null）
- 每個 Checkpoint 後執行 `dotnet build` 與 `dotnet test` 確認狀態
