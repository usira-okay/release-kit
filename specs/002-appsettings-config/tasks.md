---

description: "Task list for appsettings configuration feature"
---

# Tasks: 配置設定類別與 DI 整合

**Input**: Design documents from `/specs/002-appsettings-config/`
**Prerequisites**: plan.md, spec.md, data-model.md, research.md, quickstart.md

**Tests**: Tests are included based on TDD requirement in Constitution.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- Single project: `src/`, `tests/` at repository root
- Paths follow Clean Architecture structure (Domain → Application → Infrastructure → Console)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [X] T001 Verify project structure exists in src/ReleaseKit.Infrastructure/Configuration/ and tests/ReleaseKit.Infrastructure.Tests/Configuration/
- [X] T002 [P] Install required NuGet packages: Microsoft.Extensions.Configuration, Microsoft.Extensions.Options, Microsoft.Extensions.Options.ConfigurationExtensions (if not already installed)
- [X] T003 [P] Install test packages: xUnit, FluentAssertions, Microsoft.Extensions.Configuration.Json (if not already installed)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 Create ServiceCollectionExtensions.cs in src/ReleaseKit.Console/Extensions/ with AddConfigurationOptions method (if not exists)
- [X] T005 Create appsettings.json in src/ReleaseKit.Console/ (if not exists) with empty configuration sections

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - 開發者新增配置設定 (Priority: P1) 🎯 MVP

**Goal**: 建立強型別配置類別系統，支援 appsettings.json 映射至 Options 類別，並透過 DI 容器注入使用

**Independent Test**: 
1. 在 appsettings.json 中定義配置區段
2. 建立對應的 Options 類別
3. 註冊至 DI 容器
4. 在服務中注入 IOptions<T> 並讀取配置值
5. 驗證配置值正確綁定

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T006 [P] [US1] Create GoogleSheetOptionsTests.cs in tests/ReleaseKit.Infrastructure.Tests/Configuration/ with test: Bind_ValidConfiguration_ShouldBindCorrectly
- [X] T007 [P] [US1] Create AzureDevOpsOptionsTests.cs in tests/ReleaseKit.Infrastructure.Tests/Configuration/ with test: Bind_ValidConfiguration_ShouldBindCorrectly
- [X] T008 [P] [US1] Create GitLabOptionsTests.cs in tests/ReleaseKit.Infrastructure.Tests/Configuration/ with test: Bind_ValidConfiguration_ShouldBindCorrectly
- [X] T009 [P] [US1] Create BitbucketOptionsTests.cs in tests/ReleaseKit.Infrastructure.Tests/Configuration/ with test: Bind_ValidConfiguration_ShouldBindCorrectly
- [X] T010 [P] [US1] Create FetchModeOptionsTests.cs in tests/ReleaseKit.Infrastructure.Tests/Configuration/ with test: Bind_ValidConfiguration_ShouldBindCorrectly

### Implementation for User Story 1

#### Root Level Options

- [X] T011 [P] [US1] Create FetchModeOptions.cs in src/ReleaseKit.Infrastructure/Configuration/ with properties: FetchMode, SourceBranch, StartDateTime, EndDateTime (with Data Annotations)

#### Google Sheet Options

- [X] T012 [P] [US1] Create ColumnMappingOptions.cs in src/ReleaseKit.Infrastructure/Configuration/ with properties: RepositoryNameColumn, FeatureColumn, TeamColumn, AuthorsColumn, PullRequestUrlsColumn, UniqueKeyColumn, AutoSyncColumn (with RegularExpression validation)
- [X] T013 [US1] Create GoogleSheetOptions.cs in src/ReleaseKit.Infrastructure/Configuration/ with properties: SpreadsheetId, SheetName, ServiceAccountCredentialPath, ColumnMapping (depends on T012)

#### Azure DevOps Options

- [X] T014 [P] [US1] Create TeamMappingOptions.cs in src/ReleaseKit.Infrastructure/Configuration/ with properties: OriginalTeamName, DisplayName (with Data Annotations)
- [X] T015 [US1] Create AzureDevOpsOptions.cs in src/ReleaseKit.Infrastructure/Configuration/ with properties: OrganizationUrl, TeamMapping (depends on T014)

#### GitLab Options

- [X] T016 [P] [US1] Create GitLabProjectOptions.cs in src/ReleaseKit.Infrastructure/Configuration/ with properties: ProjectPath, TargetBranch, FetchMode, SourceBranch, StartDateTime, EndDateTime (with Data Annotations)
- [X] T017 [US1] Create GitLabOptions.cs in src/ReleaseKit.Infrastructure/Configuration/ with properties: ApiUrl, AccessToken, Projects (depends on T016)

#### Bitbucket Options

- [X] T018 [P] [US1] Create BitbucketProjectOptions.cs in src/ReleaseKit.Infrastructure/Configuration/ with properties: ProjectPath, TargetBranch, FetchMode, SourceBranch, StartDateTime, EndDateTime (same structure as GitLabProjectOptions)
- [X] T019 [US1] Create BitbucketOptions.cs in src/ReleaseKit.Infrastructure/Configuration/ with properties: ApiUrl, Email, AccessToken, Projects (depends on T018)

#### DI Registration

- [X] T020 [US1] Register FetchModeOptions in AddConfigurationOptions method in src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs with ValidateDataAnnotations and ValidateOnStart
- [X] T021 [US1] Register GoogleSheetOptions in AddConfigurationOptions method in src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs with ValidateDataAnnotations and ValidateOnStart
- [X] T022 [US1] Register AzureDevOpsOptions in AddConfigurationOptions method in src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs with ValidateDataAnnotations and ValidateOnStart
- [X] T023 [US1] Register GitLabOptions in AddConfigurationOptions method in src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs with ValidateDataAnnotations and ValidateOnStart
- [X] T024 [US1] Register BitbucketOptions in AddConfigurationOptions method in src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs with ValidateDataAnnotations and ValidateOnStart

#### Configuration File

- [X] T025 [US1] Add FetchMode, GoogleSheet, AzureDevOps, GitLab, Bitbucket sections to appsettings.json in src/ReleaseKit.Console/ with example values

#### Test Verification

- [X] T026 [US1] Run all configuration tests to verify Options classes bind correctly

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently - all Options classes can be injected and used in services

---

## Phase 4: User Story 2 - 開發者驗證必要配置 (Priority: P2)

**Goal**: 確保必要的配置項目在應用程式啟動時存在且有效，若缺少必要配置應立即失敗並提供明確錯誤訊息

**Independent Test**: 
1. 移除 appsettings.json 中的必要配置項目（如 BaseUrl）
2. 啟動應用程式
3. 驗證是否在 1 秒內拋出 InvalidOperationException
4. 驗證錯誤訊息是否包含缺少的配置鍵名稱

### Tests for User Story 2 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T027 [P] [US2] Add test: Validate_MissingRequiredProperty_ShouldThrowException to GoogleSheetOptionsTests.cs in tests/ReleaseKit.Infrastructure.Tests/Configuration/
- [X] T028 [P] [US2] Add test: Validate_MissingRequiredProperty_ShouldThrowException to AzureDevOpsOptionsTests.cs in tests/ReleaseKit.Infrastructure.Tests/Configuration/
- [X] T029 [P] [US2] Add test: Validate_MissingRequiredProperty_ShouldThrowException to GitLabOptionsTests.cs in tests/ReleaseKit.Infrastructure.Tests/Configuration/
- [X] T030 [P] [US2] Add test: Validate_MissingRequiredProperty_ShouldThrowException to BitbucketOptionsTests.cs in tests/ReleaseKit.Infrastructure.Tests/Configuration/
- [X] T031 [P] [US2] Add test: Validate_MissingRequiredProperty_ShouldThrowException to FetchModeOptionsTests.cs in tests/ReleaseKit.Infrastructure.Tests/Configuration/

### Implementation for User Story 2

- [X] T032 [P] [US2] Verify all [Required] attributes are correctly applied in FetchModeOptions.cs
- [X] T033 [P] [US2] Verify all [Required] attributes are correctly applied in GoogleSheetOptions.cs and ColumnMappingOptions.cs
- [X] T034 [P] [US2] Verify all [Required] attributes are correctly applied in AzureDevOpsOptions.cs and TeamMappingOptions.cs
- [X] T035 [P] [US2] Verify all [Required] attributes are correctly applied in GitLabOptions.cs and GitLabProjectOptions.cs
- [X] T036 [P] [US2] Verify all [Required] attributes are correctly applied in BitbucketOptions.cs and BitbucketProjectOptions.cs
- [X] T037 [US2] Verify all Options registrations use ValidateOnStart() in AddConfigurationOptions method in src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs
- [X] T038 [US2] Run all validation tests to verify OptionsValidationException is thrown for missing required properties

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently - configuration validation fails fast at startup with clear error messages

---

## Phase 5: User Story 3 - 開發者透過環境變數覆寫配置 (Priority: P3)

**Goal**: 開發者需要在不同環境中透過環境變數覆寫 appsettings.json 中的敏感配置（如 API Token），而不需修改檔案

**Independent Test**: 
1. 設定環境變數（如 GitLab__ApiUrl=https://custom.gitlab.com）
2. 啟動應用程式
3. 驗證注入的 Options 實例中的值是否被環境變數覆寫

### Tests for User Story 3 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T039 [P] [US3] Add test: Bind_EnvironmentVariableOverride_ShouldUseEnvironmentValue to GoogleSheetOptionsTests.cs in tests/ReleaseKit.Infrastructure.Tests/Configuration/
- [ ] T040 [P] [US3] Add test: Bind_EnvironmentVariableOverride_ShouldUseEnvironmentValue to AzureDevOpsOptionsTests.cs in tests/ReleaseKit.Infrastructure.Tests/Configuration/
- [ ] T041 [P] [US3] Add test: Bind_EnvironmentVariableOverride_ShouldUseEnvironmentValue to GitLabOptionsTests.cs in tests/ReleaseKit.Infrastructure.Tests/Configuration/
- [ ] T042 [P] [US3] Add test: Bind_EnvironmentVariableOverride_ShouldUseEnvironmentValue to BitbucketOptionsTests.cs in tests/ReleaseKit.Infrastructure.Tests/Configuration/
- [ ] T043 [P] [US3] Add test: Bind_EnvironmentVariableOverride_ShouldUseEnvironmentValue to FetchModeOptionsTests.cs in tests/ReleaseKit.Infrastructure.Tests/Configuration/

### Implementation for User Story 3

- [X] T044 [US3] Verify ConfigurationBuilder includes AddEnvironmentVariables() in Program.cs in src/ReleaseKit.Console/
- [ ] T045 [US3] Run all environment variable override tests to verify environment variables correctly override JSON configuration

**Checkpoint**: All user stories should now be independently functional - configuration can be overridden via environment variables

---

## Phase 6: User Story 4 - 支援條件驗證 (Bonus - 非必要，從 spec.md 的 data-model.md 推論)

**Goal**: 實作跨屬性驗證邏輯（如 FetchMode 為 BranchDiff 時，SourceBranch 必須提供）

**Independent Test**: 
1. 設定 FetchMode = "BranchDiff" 但不提供 SourceBranch
2. 啟動應用程式
3. 驗證是否拋出驗證錯誤

### Tests for User Story 4 ⚠️

- [ ] T046 [P] [US4] Add test: Validate_BranchDiffModeWithoutSourceBranch_ShouldFail to FetchModeOptionsTests.cs in tests/ReleaseKit.Infrastructure.Tests/Configuration/
- [ ] T047 [P] [US4] Add test: Validate_DateTimeRangeModeWithoutDates_ShouldFail to FetchModeOptionsTests.cs in tests/ReleaseKit.Infrastructure.Tests/Configuration/
- [ ] T048 [P] [US4] Add test: Validate_BranchDiffModeWithoutSourceBranch_ShouldFail to GitLabOptionsTests.cs in tests/ReleaseKit.Infrastructure.Tests/Configuration/
- [ ] T049 [P] [US4] Add test: Validate_BranchDiffModeWithoutSourceBranch_ShouldFail to BitbucketOptionsTests.cs in tests/ReleaseKit.Infrastructure.Tests/Configuration/

### Implementation for User Story 4

- [ ] T050 [US4] Implement IValidatableObject interface in FetchModeOptions.cs in src/ReleaseKit.Infrastructure/Configuration/ with conditional validation logic
- [ ] T051 [US4] Implement IValidatableObject interface in GitLabProjectOptions.cs in src/ReleaseKit.Infrastructure/Configuration/ with conditional validation logic
- [ ] T052 [US4] Implement IValidatableObject interface in BitbucketProjectOptions.cs in src/ReleaseKit.Infrastructure/Configuration/ with conditional validation logic
- [ ] T053 [US4] Run all conditional validation tests to verify cross-property validation works correctly

**Checkpoint**: All user stories including bonus features should now be functional

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T054 [P] Add XML documentation comments to all Options classes in src/ReleaseKit.Infrastructure/Configuration/
- [X] T055 [P] Verify all file names match class names (Constitution requirement)
- [ ] T056 [P] Add quickstart.md validation examples to README.md or feature documentation
- [X] T057 Run full test suite to ensure all user stories pass independently
- [ ] T058 Code review: Verify all Options classes follow Constitution (TDD, SOLID, Result Pattern not applicable here)
- [ ] T059 Final integration test: Start application with complete appsettings.json and verify all Options are correctly injected

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-6)**: All depend on Foundational phase completion
  - User stories can then proceed in parallel (if staffed)
  - Or sequentially in priority order (US1 → US2 → US3 → US4)
- **Polish (Phase 7)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - Extends US1 validation logic but independently testable
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) - Extends US1 configuration binding but independently testable
- **User Story 4 (Bonus)**: Can start after Foundational (Phase 2) - Extends US2 validation logic but independently testable

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Nested Options classes before parent Options classes (e.g., ColumnMappingOptions before GoogleSheetOptions)
- Options classes before DI registration
- DI registration before configuration file updates
- All implementation before test verification

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel
- All test creation tasks marked [P] within the same user story can run in parallel
- All Options class creation tasks marked [P] within the same user story can run in parallel (if they don't depend on nested classes)
- Different user stories can be worked on in parallel by different team members after Foundational phase

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together:
Task: "Create GoogleSheetOptionsTests.cs" (T006)
Task: "Create AzureDevOpsOptionsTests.cs" (T007)
Task: "Create GitLabOptionsTests.cs" (T008)
Task: "Create BitbucketOptionsTests.cs" (T009)
Task: "Create FetchModeOptionsTests.cs" (T010)

# Launch all root-level and independent nested Options classes together:
Task: "Create FetchModeOptions.cs" (T011)
Task: "Create ColumnMappingOptions.cs" (T012)
Task: "Create TeamMappingOptions.cs" (T014)
Task: "Create GitLabProjectOptions.cs" (T016)
Task: "Create BitbucketProjectOptions.cs" (T018)

# Then launch parent Options classes (depends on nested classes):
Task: "Create GoogleSheetOptions.cs" (T013)
Task: "Create AzureDevOpsOptions.cs" (T015)
Task: "Create GitLabOptions.cs" (T017)
Task: "Create BitbucketOptions.cs" (T019)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test User Story 1 independently (inject Options in a service and verify values)
5. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!)
3. Add User Story 2 → Test independently → Deploy/Demo
4. Add User Story 3 → Test independently → Deploy/Demo
5. Add User Story 4 (Bonus) → Test independently → Deploy/Demo
6. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1 (T006-T026)
   - Developer B: User Story 2 (T027-T038) - can start after US1 Options classes exist
   - Developer C: User Story 3 (T039-T045) - can start after US1 Options classes exist
3. Stories complete and integrate independently

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Verify tests fail before implementing (TDD Red-Green-Refactor)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- All Options classes use `record` or `class` with `init` properties (immutability)
- All validation uses Data Annotations + IValidatableObject (no try-catch)
- All comments and documentation in Traditional Chinese (zh-tw)
