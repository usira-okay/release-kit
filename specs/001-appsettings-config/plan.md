# Implementation Plan: AppSettings 配置擴充

**Branch**: `001-appsettings-config` | **Date**: 2026-01-28 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-appsettings-config/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

擴充 appsettings.json 配置結構，新增 FetchMode、GoogleSheet、AzureDevOps 與 GitLab 專案層級配置，並建立對應的強型別配置類別。所有配置類別將透過 Options Pattern 註冊至 DI 容器，並在應用程式啟動時驗證必要欄位。

## Technical Context

**Language/Version**: C# / .NET 9.0  
**Primary Dependencies**: Microsoft.Extensions.Configuration, Microsoft.Extensions.Options, Microsoft.Extensions.DependencyInjection  
**Storage**: N/A (配置管理不涉及持久化儲存)  
**Testing**: xUnit, FluentAssertions (專案已採用)  
**Target Platform**: Linux/Windows Console Application  
**Project Type**: Single (Console Application with Clean Architecture layers)  
**Performance Goals**: 配置載入時間 < 100ms  
**Constraints**: 
- 必要配置未設定時必須在啟動階段立即失敗
- 配置類別必須為 strongly-typed，避免使用 Dictionary 或動態型別
- 禁止在執行時修改配置（immutable after startup）
**Scale/Scope**: 
- 新增 4 個配置區段（FetchMode, GoogleSheet, AzureDevOps, GitLab.Projects）
- 預計新增 5-7 個配置類別
- 影響範圍：ReleaseKit.Console 層級（Options 資料夾與 DI 註冊）

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### 強制性規範檢查

| 規範 | 檢查項目 | 狀態 | 備註 |
|------|---------|------|------|
| **I. TDD** | 是否需要為配置類別撰寫單元測試？ | ✅ PASS | 需為配置綁定與驗證邏輯撰寫測試 |
| **II. DDD/CQRS** | 配置類別是否屬於 Domain 層？ | ✅ PASS | 配置類別屬於 Infrastructure 關注點，放置於 Console 層的 Options 資料夾 |
| **III. SOLID** | 配置類別是否遵循單一職責？ | ✅ PASS | 每個配置類別對應單一配置區段 |
| **V. 錯誤處理** | 是否使用 Result Pattern？ | ⚠️ WAIVED | 配置驗證在啟動階段執行，允許直接拋出 InvalidOperationException |
| **VII. 避免硬編碼** | 配置鍵值名稱是否硬編碼？ | ✅ PASS | 配置鍵值透過 Options Pattern 自動綁定 |
| **VIII. 文件與註解** | 配置類別是否有 XML 註解？ | ✅ PASS | 所有公開屬性必須加入 summary 註解 |
| **X. Program.cs 整潔** | DI 註冊是否抽取至擴充方法？ | ✅ PASS | 使用既有的 ServiceCollectionExtensions 擴充方法 |
| **XI. 檔案組織** | 一個檔案一個類別？ | ✅ PASS | 每個配置類別獨立檔案 |

### 評估結果

**GATE STATUS**: ✅ PASS

**Waived Items**:
- **錯誤處理 (V)**: 配置驗證屬於應用程式啟動前置檢查，直接拋出例外符合 Fail-Fast 原則，不需使用 Result Pattern。

---

### Phase 1 後重新評估

**Re-evaluation Date**: 2026-01-28

所有規範檢查項目維持 ✅ PASS 狀態。設計階段確認：
- 配置類別設計符合 SOLID 原則（單一職責、介面隔離）
- 驗證邏輯採用自訂驗證方法，提供精確錯誤訊息
- DI 註冊邏輯集中於 ServiceCollectionExtensions，符合開放封閉原則
- 所有類別包含完整 XML 註解

**Final GATE STATUS**: ✅ PASS - 可進入 Phase 2 (Tasks Generation)

## Project Structure

### Documentation (this feature)

```text
specs/001-appsettings-config/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command) - N/A for this feature
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── ReleaseKit.Domain/              # 領域層（不涉及配置）
├── ReleaseKit.Application/         # 應用層（不涉及配置）
├── ReleaseKit.Infrastructure/      # 基礎設施層（不涉及配置）
└── ReleaseKit.Console/             # Console 層（配置管理所在）
    ├── Options/                    # 配置類別存放處
    │   ├── FetchModeOptions.cs     # [NEW] FetchMode 配置
    │   ├── GoogleSheetOptions.cs   # [NEW] Google Sheet 配置
    │   ├── ColumnMappingOptions.cs # [NEW] Google Sheet 欄位對應配置
    │   ├── AzureDevOpsOptions.cs   # [NEW] Azure DevOps 配置
    │   ├── TeamMappingOptions.cs   # [NEW] 團隊名稱對應配置
    │   ├── GitLabProjectOptions.cs # [MODIFIED] 擴充既有類別，新增 FetchMode 等欄位
    │   ├── GitLabOptions.cs        # [EXISTING] 既有類別，不需修改
    │   └── ...                     # 其他既有配置
    ├── Extensions/
    │   └── ServiceCollectionExtensions.cs  # [MODIFIED] 新增配置註冊邏輯
    └── Program.cs                  # [NO CHANGE] 使用既有的擴充方法

tests/
└── ReleaseKit.Console.Tests/      # [NEW] Console 層單元測試
    └── Options/                    # [NEW] 配置類別測試
        ├── GoogleSheetOptionsTests.cs
        ├── AzureDevOpsOptionsTests.cs
        └── GitLabProjectOptionsTests.cs
```

**Structure Decision**: 
- 配置類別放置於 `ReleaseKit.Console/Options` 資料夾，遵循既有慣例
- 測試專案需新增 `ReleaseKit.Console.Tests` (目前不存在)
- DI 註冊邏輯集中於 `ServiceCollectionExtensions.cs`，避免污染 Program.cs

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
