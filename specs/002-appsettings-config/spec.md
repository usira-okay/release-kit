# Feature Specification: 配置設定類別與 DI 整合

**Feature Branch**: `002-appsettings-config`  
**Created**: 2026-01-28  
**Status**: Draft  
**Input**: 替 appsettings.json 中添加設定，生成相對應的類別，並註冊進 DI 中

## User Scenarios & Testing *(mandatory)*

### User Story 1 - 開發者新增配置設定 (Priority: P1)

開發者需要在 `appsettings.json` 中添加新的配置區段,並透過強型別類別存取這些配置，以便在應用程式中使用。

**Why this priority**: 這是最基礎的功能需求，沒有配置類別就無法進行後續的功能開發。所有依賴配置的功能都需要此基礎建設。

**Independent Test**: 可透過「新增配置區段至 appsettings.json → 建立對應 Options 類別 → 註冊至 DI 容器 → 在 Console 應用中注入並讀取配置值」的完整流程獨立測試，驗證配置是否正確載入。

**Acceptance Scenarios**:

1. **Given** `appsettings.json` 存在，**When** 開發者新增配置區段（如 `"GitLab": { "BaseUrl": "..." }`），**Then** 可建立對應的 Options 類別來映射此配置結構
2. **Given** Options 類別已建立，**When** 開發者在 `Program.cs` 中註冊此類別至 DI 容器，**Then** 應用程式啟動時能正確綁定配置值到 Options 實例
3. **Given** Options 類別已註冊至 DI，**When** 應用程式中的任何服務透過建構函式注入 `IOptions<T>` 或 `IOptionsSnapshot<T>`，**Then** 能夠正確取得配置值

---

### User Story 2 - 開發者驗證必要配置 (Priority: P2)

開發者需要確保必要的配置項目在應用程式啟動時存在且有效，若缺少必要配置應立即失敗並提供明確錯誤訊息。

**Why this priority**: 配置驗證能夠在啟動階段就發現問題，避免執行時才發現配置錯誤，提升開發與除錯效率。

**Independent Test**: 可透過「移除必要配置項目 → 啟動應用程式 → 驗證是否拋出 InvalidOperationException 並包含清楚的錯誤訊息」來獨立測試配置驗證邏輯。

**Acceptance Scenarios**:

1. **Given** Options 類別定義了必要屬性（如 `BaseUrl`），**When** `appsettings.json` 中缺少此配置或值為空，**Then** 應用程式啟動時應拋出 `InvalidOperationException` 並說明缺少哪個配置鍵值
2. **Given** 配置驗證邏輯已實作，**When** 所有必要配置都正確提供，**Then** 應用程式能正常啟動且不拋出例外

---

### User Story 3 - 開發者透過環境變數覆寫配置 (Priority: P3)

開發者需要在不同環境（開發、測試、生產）中透過環境變數覆寫 `appsettings.json` 中的敏感配置（如 API Token），而不需修改檔案。

**Why this priority**: 環境變數支援是生產環境部署的標準實務，但不阻礙基本功能開發，因此優先級較低。

**Independent Test**: 可透過「設定環境變數（如 `GitLab__ApiToken=test123`）→ 啟動應用程式 → 驗證注入的 Options 實例中的值是否被環境變數覆寫」來獨立測試。

**Acceptance Scenarios**:

1. **Given** `appsettings.json` 中定義了配置值，**When** 開發者設定同名的環境變數（使用 `__` 作為階層分隔符），**Then** 環境變數的值應覆寫 JSON 檔案中的值
2. **Given** 環境變數已設定，**When** 應用程式注入 Options 實例，**Then** 應取得環境變數中的值而非 JSON 檔案中的值

---

### Edge Cases

- 配置區段存在但屬性值為 `null` 或空字串時，如何處理？
- 配置類別屬性型別與 JSON 值型別不匹配時（如字串 vs 整數），系統是否能提供明確錯誤訊息？
- 多個配置來源（appsettings.json、appsettings.Development.json、環境變數）中有相同鍵值時，優先順序為何？
- Options 類別包含複雜型別（如巢狀物件、陣列）時，綁定是否正常運作？

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: 系統 MUST 支援在 `appsettings.json` 中定義多個配置區段，每個區段可包含任意深度的階層結構
- **FR-002**: 系統 MUST 提供對應的 Options 類別來映射 `appsettings.json` 中的配置區段，使用強型別存取
- **FR-003**: 系統 MUST 在 `Program.cs` 中透過 `IServiceCollection.Configure<TOptions>()` 註冊 Options 類別至 DI 容器
- **FR-004**: 系統 MUST 支援在任何服務中透過建構函式注入 `IOptions<TOptions>`、`IOptionsSnapshot<TOptions>` 或 `IOptionsMonitor<TOptions>` 來存取配置值
- **FR-005**: 系統 MUST 驗證必要的配置項目在應用程式啟動時存在且非空，若缺少則拋出 `InvalidOperationException` 並包含清楚的錯誤訊息（指明缺少哪個配置鍵值）
- **FR-006**: 系統 MUST 支援透過環境變數覆寫 `appsettings.json` 中的配置值，使用 `__` 作為階層分隔符（例如 `Section__SubSection__Key`）
- **FR-007**: Options 類別 MUST 使用 `record` 或 `class` 定義，所有屬性使用 `init` 或 `required` 修飾符以確保不可變性與必要性
- **FR-008**: Options 類別 MUST 包含參數驗證邏輯（透過 Data Annotations 或自訂驗證），在綁定時自動驗證配置有效性

### Key Entities *(include if feature involves data)*

- **Options 類別**: 強型別配置類別，映射 `appsettings.json` 中的特定區段，包含對應的屬性與驗證邏輯。範例：`GitLabOptions`、`GoogleSheetsOptions`
  - 關鍵屬性範例：`BaseUrl`（string）、`ApiToken`（string）、`ProjectId`（int）、`Timeout`（TimeSpan）
  
- **Configuration 區段**: `appsettings.json` 中的 JSON 物件，定義系統運作所需的參數，可包含多層巢狀結構

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 開發者能在 5 分鐘內完成「新增配置區段 → 建立 Options 類別 → 註冊至 DI → 注入使用」的完整流程
- **SC-002**: 當缺少必要配置時，應用程式在啟動階段立即失敗（1 秒內），並在 Console 輸出明確的錯誤訊息（包含缺少的配置鍵名稱）
- **SC-003**: 所有 Options 類別透過強型別存取，編譯時期即可發現屬性名稱錯誤（100% 型別安全）
- **SC-004**: 環境變數能成功覆寫 JSON 檔案中的配置值，測試覆蓋率達 100%（所有配置項目都可被覆寫）

## Assumptions

- 假設使用 .NET 9 內建的 `Microsoft.Extensions.Configuration` 與 `Microsoft.Extensions.Options` 套件，無需額外安裝第三方配置管理套件
- 假設配置檔案格式固定為 JSON，不考慮支援 YAML 或 XML 等其他格式
- 假設配置值的型別轉換由 .NET 內建的 Configuration Binder 處理，支援常見型別（string、int、bool、TimeSpan 等）
- 假設環境變數的優先順序高於 `appsettings.json`，符合 .NET Configuration 的預設行為
- 假設開發者熟悉 ASP.NET Core 的 Options Pattern，本功能僅提供基礎建設而不包含教學文件

## Out of Scope

- 不包含配置值的加密與解密功能（敏感資訊應透過環境變數或 Secret Manager 管理）
- 不包含配置值的動態重新載入（Hot Reload）功能，應用程式啟動後配置即固定
- 不包含配置值的遠端載入（如從 Azure App Configuration 或 Consul 載入）
- 不包含配置檔案的版本控制與變更追蹤
- 不包含配置值的單元測試輔助工具（如 Mock Options）

## Dependencies & Risks

### Dependencies

- 依賴 `Microsoft.Extensions.Configuration` NuGet 套件（.NET 9 內建）
- 依賴 `Microsoft.Extensions.Options` NuGet 套件（.NET 9 內建）
- 依賴 `Microsoft.Extensions.Options.ConfigurationExtensions` NuGet 套件（.NET 9 內建）

### Risks

- **風險 1**: 配置區段名稱與 Options 類別綁定時大小寫敏感，可能導致綁定失敗且不易除錯
  - **緩解措施**: 在文件中明確說明綁定規則，並在測試中涵蓋大小寫場景
  
- **風險 2**: 環境變數使用 `__` 作為階層分隔符，在某些 Shell 環境中可能有特殊意義
  - **緩解措施**: 在文件中提供不同作業系統的環境變數設定範例

- **風險 3**: Options 類別屬性型別錯誤時，Configuration Binder 可能靜默失敗（屬性保持預設值而不拋出例外）
  - **緩解措施**: 使用 `required` 修飾符強制必要屬性，並在啟動時驗證配置完整性
