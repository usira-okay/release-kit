---
description: "Task list for AppSettings 配置擴充 feature implementation"
---

# Tasks: AppSettings 配置擴充

**Input**: Design documents from `/mnt/c/SourceCode/release-kit/specs/001-appsettings-config/`
**Prerequisites**: plan.md, spec.md, data-model.md, research.md, quickstart.md

**Tests**: Tests are OPTIONAL per project constitution - only included for configuration validation logic.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Console project**: `src/ReleaseKit.Console/`
- **Test project**: `tests/ReleaseKit.Console.Tests/` (需新建)
- Paths shown below follow Clean Architecture structure

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [ ] T001 Create test project ReleaseKit.Console.Tests under tests/ directory
- [ ] T002 Add project reference from ReleaseKit.Console.Tests to ReleaseKit.Console
- [ ] T003 Install xUnit and FluentAssertions packages in ReleaseKit.Console.Tests

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T004 [P] Create FetchMode enum in src/ReleaseKit.Console/Options/FetchMode.cs
- [ ] T005 [P] Create GoogleSheetOptions class in src/ReleaseKit.Console/Options/GoogleSheetOptions.cs
- [ ] T006 [P] Create ColumnMappingOptions class in src/ReleaseKit.Console/Options/ColumnMappingOptions.cs
- [ ] T007 [P] Create AzureDevOpsOptions class in src/ReleaseKit.Console/Options/AzureDevOpsOptions.cs
- [ ] T008 [P] Create TeamMappingOptions class in src/ReleaseKit.Console/Options/TeamMappingOptions.cs
- [ ] T009 Modify GitLabProjectOptions class in src/ReleaseKit.Console/Options/GitLabProjectOptions.cs to add FetchMode, SourceBranch, StartDateTime, EndDateTime fields

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - 定義新的應用程式配置區段 (Priority: P1) 🎯 MVP

**Goal**: 開發者可在 appsettings.json 中新增配置區段，並透過強型別類別存取這些配置，配置資料可透過依賴注入取得，並在應用程式啟動時驗證必要欄位。

**Independent Test**: 
1. 在 appsettings.json 中新增配置區段
2. 建立對應的強型別配置類別
3. 在 Program.cs 中註冊配置至 DI 容器
4. 透過單元測試驗證配置可被正確注入並讀取

### Implementation for User Story 1

- [ ] T010 [P] [US1] Add Validate() method to GoogleSheetOptions in src/ReleaseKit.Console/Options/GoogleSheetOptions.cs
- [ ] T011 [P] [US1] Add Validate() method to ColumnMappingOptions in src/ReleaseKit.Console/Options/ColumnMappingOptions.cs
- [ ] T012 [P] [US1] Add Validate() method to AzureDevOpsOptions in src/ReleaseKit.Console/Options/AzureDevOpsOptions.cs
- [ ] T013 [P] [US1] Add Validate() method to TeamMappingOptions in src/ReleaseKit.Console/Options/TeamMappingOptions.cs
- [ ] T014 [P] [US1] Add Validate() method to GitLabProjectOptions in src/ReleaseKit.Console/Options/GitLabProjectOptions.cs
- [ ] T015 [US1] Register GoogleSheetOptions to DI container in src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs with ValidateOnStart
- [ ] T016 [US1] Register AzureDevOpsOptions to DI container in src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs with ValidateOnStart
- [ ] T017 [P] [US1] Create GoogleSheetOptionsTests in tests/ReleaseKit.Console.Tests/Options/GoogleSheetOptionsTests.cs
- [ ] T018 [P] [US1] Create AzureDevOpsOptionsTests in tests/ReleaseKit.Console.Tests/Options/AzureDevOpsOptionsTests.cs
- [ ] T019 [P] [US1] Create GitLabProjectOptionsTests in tests/ReleaseKit.Console.Tests/Options/GitLabProjectOptionsTests.cs

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - 環境特定配置覆寫 (Priority: P2)

**Goal**: 開發者可針對不同環境（Development、Production）提供不同的配置值，並在特定環境中覆寫基礎配置。

**Independent Test**: 
1. 建立 appsettings.Development.json 和 appsettings.Production.json
2. 在不同環境中覆寫特定配置值
3. 在對應環境中啟動應用程式
4. 驗證配置值是否正確反映環境特定的覆寫

### Implementation for User Story 2

- [ ] T020 [P] [US2] Create appsettings.Development.json in src/ReleaseKit.Console/ with sample environment-specific overrides
- [ ] T021 [P] [US2] Create appsettings.Production.json in src/ReleaseKit.Console/ with sample environment-specific overrides
- [ ] T022 [US2] Update Program.cs in src/ReleaseKit.Console/Program.cs to ensure environment-specific configuration loading order
- [ ] T023 [US2] Create integration test for environment-specific configuration in tests/ReleaseKit.Console.Tests/Integration/EnvironmentConfigurationTests.cs
- [ ] T024 [US2] Document environment-specific configuration usage in quickstart.md

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - 敏感資訊透過環境變數注入 (Priority: P3)

**Goal**: 開發者可透過環境變數注入敏感配置（如 API Token、連線字串），而不將這些資訊寫入 appsettings.json 檔案中。

**Independent Test**: 
1. 設定環境變數（例如 `ReleaseKit__GitLab__Token`）
2. 啟動應用程式
3. 驗證配置類別中的對應屬性值來自環境變數而非 appsettings.json

### Implementation for User Story 3

- [ ] T025 [US3] Verify environment variable override mechanism in Program.cs in src/ReleaseKit.Console/Program.cs
- [ ] T026 [US3] Create integration test for environment variable override in tests/ReleaseKit.Console.Tests/Integration/EnvironmentVariableOverrideTests.cs
- [ ] T027 [US3] Document environment variable naming conventions in quickstart.md
- [ ] T028 [US3] Add examples of sensitive information injection via environment variables in quickstart.md

**Checkpoint**: All user stories should now be independently functional

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T029 [P] Add XML documentation comments to all public properties in Options classes
- [ ] T030 [P] Update constitution.md validation status in specs/001-appsettings-config/plan.md
- [ ] T031 Validate all scenarios in quickstart.md by running the application
- [ ] T032 Run existing tests to ensure no regression with new configuration changes
- [ ] T033 [P] Update README.md with configuration usage instructions if needed

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-5)**: All depend on Foundational phase completion
  - User stories can then proceed in parallel (if staffed)
  - Or sequentially in priority order (P1 → P2 → P3)
- **Polish (Phase 6)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - Builds on US1 but can be tested independently
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) - Builds on US1 but can be tested independently

### Within Each User Story

- Validation methods before DI registration
- DI registration before integration tests
- Unit tests can be written in parallel with implementation
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks (T001-T003) can run in parallel
- All Foundational tasks marked [P] (T004-T008) can run in parallel
- Once Foundational phase completes, validation methods (T010-T014) can be written in parallel
- Unit tests (T017-T019) can be written in parallel
- Environment-specific configuration files (T020-T021) can be created in parallel
- Documentation tasks (T029, T033) can run in parallel
- Different user stories can be worked on in parallel by different team members

---

## Parallel Example: User Story 1

```bash
# Launch all validation method implementations together:
Task: "Add Validate() method to GoogleSheetOptions in src/ReleaseKit.Console/Options/GoogleSheetOptions.cs"
Task: "Add Validate() method to ColumnMappingOptions in src/ReleaseKit.Console/Options/ColumnMappingOptions.cs"
Task: "Add Validate() method to AzureDevOpsOptions in src/ReleaseKit.Console/Options/AzureDevOpsOptions.cs"
Task: "Add Validate() method to TeamMappingOptions in src/ReleaseKit.Console/Options/TeamMappingOptions.cs"
Task: "Add Validate() method to GitLabProjectOptions in src/ReleaseKit.Console/Options/GitLabProjectOptions.cs"

# Launch all unit tests together:
Task: "Create GoogleSheetOptionsTests in tests/ReleaseKit.Console.Tests/Options/GoogleSheetOptionsTests.cs"
Task: "Create AzureDevOpsOptionsTests in tests/ReleaseKit.Console.Tests/Options/AzureDevOpsOptionsTests.cs"
Task: "Create GitLabProjectOptionsTests in tests/ReleaseKit.Console.Tests/Options/GitLabProjectOptionsTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test User Story 1 independently
5. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!)
3. Add User Story 2 → Test independently → Deploy/Demo
4. Add User Story 3 → Test independently → Deploy/Demo
5. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1 (Configuration binding & validation)
   - Developer B: User Story 2 (Environment-specific overrides)
   - Developer C: User Story 3 (Environment variable injection)
3. Stories complete and integrate independently

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence
- All configuration classes follow Options Pattern with IOptions<T> injection
- Validation uses ValidateOnStart to ensure fail-fast behavior
- Tests validate both successful binding and validation failure scenarios
