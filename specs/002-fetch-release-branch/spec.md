# Feature Specification: 取得各 Repository 最新 Release Branch 名稱

**Feature Branch**: `002-fetch-release-branch`
**Created**: 2026-02-10
**Status**: Draft
**Input**: User description: "新增指令取得各 repository 最新 release branch 名稱，取得後將資料塞入 Redis 中。依照目前 GitLab 與 Bitbucket 建立以及執行 Task 的設計模式來實作。GitLab 與 Bitbucket 的 Redis 請區分兩把 key 儲存資料。"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - 取得 GitLab 各專案最新 Release Branch (Priority: P1)

身為發佈管理人員，我想要一個指令來快速查詢所有 GitLab 專案目前最新的 release branch 名稱，以便在準備發佈時掌握各專案的版本狀態。

**Why this priority**: 這是核心功能，直接對應需求目標。取得 release branch 資訊是後續所有發佈流程的基礎。

**Independent Test**: 可透過執行 `fetch-gitlab-release-branch` 指令，驗證系統是否正確查詢各 GitLab 專案的 release branch，並將結果以正確格式存入 Redis。

**Acceptance Scenarios**:

1. **Given** 設定檔中已配置多個 GitLab 專案路徑，**When** 使用者執行取得 GitLab release branch 指令，**Then** 系統針對每個專案查詢以 `release/` 為前綴的分支，取得最新（字母排序最大）的 release branch 名稱，並依分支名稱分組輸出結果。
2. **Given** 某些 GitLab 專案不存在任何 release branch，**When** 使用者執行取得 GitLab release branch 指令，**Then** 這些專案路徑被歸類到 `NotFound` 群組中。
3. **Given** 指令執行成功，**When** 結果輸出後，**Then** 系統將 JSON 結果同時輸出到 Console 並存入 Redis（使用 GitLab 專屬 Redis Key）。

---

### User Story 2 - 取得 Bitbucket 各專案最新 Release Branch (Priority: P1)

身為發佈管理人員，我想要一個指令來快速查詢所有 Bitbucket 專案目前最新的 release branch 名稱，功能與 GitLab 對稱。

**Why this priority**: 與 User Story 1 同等重要，為平台對稱性需求。組織同時使用 GitLab 與 Bitbucket，兩者需要相同功能。

**Independent Test**: 可透過執行 `fetch-bitbucket-release-branch` 指令，驗證系統是否正確查詢各 Bitbucket 專案的 release branch，並將結果存入 Redis（使用 Bitbucket 專屬 Redis Key）。

**Acceptance Scenarios**:

1. **Given** 設定檔中已配置多個 Bitbucket 專案路徑，**When** 使用者執行取得 Bitbucket release branch 指令，**Then** 系統針對每個專案查詢以 `release/` 為前綴的分支，取得最新的 release branch 名稱，並依分支名稱分組輸出結果。
2. **Given** 某些 Bitbucket 專案不存在任何 release branch，**When** 使用者執行取得 Bitbucket release branch 指令，**Then** 這些專案路徑被歸類到 `NotFound` 群組中。
3. **Given** 指令執行成功，**When** 結果輸出後，**Then** 系統將 JSON 結果同時輸出到 Console 並存入 Redis（使用 Bitbucket 專屬 Redis Key）。

---

### User Story 3 - 查詢結果依 Release Branch 名稱分組 (Priority: P2)

身為發佈管理人員，我希望查詢結果能依 release branch 名稱分組呈現，讓我一眼看出哪些專案在相同版本、哪些專案落後或找不到 release branch。

**Why this priority**: 分組呈現是資料可讀性的關鍵需求，但核心功能不依賴於此。

**Independent Test**: 驗證輸出 JSON 的結構是否為 `{ "release/YYYYMMDD": ["ProjectPath1", ...], "NotFound": ["ProjectPath6", ...] }` 格式。

**Acceptance Scenarios**:

1. **Given** 多個專案的最新 release branch 名稱相同（如 `release/20260101`），**When** 查詢結果產出，**Then** 這些專案路徑被歸類在同一個 release branch 名稱的陣列下。
2. **Given** 不同專案有不同的最新 release branch 名稱，**When** 查詢結果產出，**Then** 每個不同的 release branch 名稱各自對應一組專案路徑。
3. **Given** 某些專案查詢 release branch 失敗或無任何 release branch，**When** 查詢結果產出，**Then** 這些專案被歸類在 `NotFound` 群組中。

---

### Edge Cases

- 專案路徑無效或無法存取時（權限不足、網路錯誤），該專案應歸類到 `NotFound` 群組，不應中斷其他專案的查詢。
- 專案設定清單為空時，系統應正常執行並輸出空的結果。
- Redis 連線失敗時，JSON 結果仍應輸出到 Console，並記錄 Redis 寫入失敗的警告日誌。
- 單一專案存在多個 release branch（如 `release/20260101`、`release/20260106`）時，只取最新（字母排序最大）的那一個。

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: 系統必須提供兩個新的 CLI 指令：一個用於查詢 GitLab 各專案的最新 release branch，一個用於查詢 Bitbucket 各專案的最新 release branch。
- **FR-002**: 系統必須針對每個已設定的專案，查詢所有以 `release/` 為前綴的分支名稱。
- **FR-003**: 系統必須從查詢到的 release branch 清單中，選取字母排序最大的分支作為該專案的「最新 release branch」。
- **FR-004**: 系統必須將查詢結果依 release branch 名稱分組，每個分組包含具有該 release branch 的所有專案路徑。
- **FR-005**: 當專案不存在任何 release branch，或查詢過程中發生錯誤時，系統必須將該專案路徑歸類到 `NotFound` 群組中。
- **FR-006**: 系統必須將結果以 JSON 格式輸出到 Console。
- **FR-007**: 系統必須將結果存入 Redis，且 GitLab 與 Bitbucket 使用不同的 Redis Key 儲存。
- **FR-008**: 執行任務前，系統必須檢查並清除 Redis 中該 Key 的舊資料（與現有 PR 拉取任務一致的行為）。
- **FR-009**: 系統必須遵循現有的 Task 設計模式（ITask 介面、TaskFactory、TaskType、CommandLineParser 註冊）。
- **FR-010**: 系統必須複用現有 `ISourceControlRepository.GetBranchesAsync` 方法來查詢分支。
- **FR-011**: 系統必須沿用現有的設定檔結構讀取平台連線資訊及專案清單。

### Key Entities

- **ReleaseBranchResult**: 代表查詢結果的整體輸出，以 Dictionary 形式呈現，key 為 release branch 名稱（或 `NotFound`），value 為對應的專案路徑清單。

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 使用者執行單一指令即可取得所有已設定專案的最新 release branch 資訊。
- **SC-002**: 所有已設定專案的查詢結果在單次執行中完成，無須多次手動操作。
- **SC-003**: 結果正確存入 Redis，後續流程可直接從 Redis 讀取資料。
- **SC-004**: 單一專案查詢失敗不影響其他專案的查詢結果，整體任務能順利完成。

## Assumptions

- Release branch 的命名慣例為 `release/YYYYMMDD` 格式（如 `release/20260101`），因此字母排序最大的分支即為最新的 release branch。
- 此功能複用現有 `appsettings.json` 中的 GitLab 和 Bitbucket 專案清單設定，不需額外的專案清單配置。
- 此功能不需要 `FetchMode`、`StartDateTime`、`EndDateTime` 等時間相關參數，只需平台連線資訊和專案路徑。
- 輸出的 JSON 格式與使用者提供的範例一致，為 `{ "release/YYYYMMDD": ["ProjectPath1", ...], "NotFound": [...] }` 結構。
