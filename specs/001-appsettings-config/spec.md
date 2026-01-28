# Feature Specification: AppSettings 配置擴充

**Feature Branch**: `001-appsettings-config`  
**Created**: 2026-01-28  
**Status**: Draft  
**Input**: User description: "替 appsettings.json 添加設定並生成對應類別"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - 定義新的應用程式配置區段 (Priority: P1)

開發者需要在 appsettings.json 中新增配置區段，並透過強型別類別存取這些配置，以便在應用程式中使用。配置資料應可透過依賴注入取得，並在應用程式啟動時驗證必要欄位。

**Why this priority**: 這是最基礎的需求，所有後續功能都依賴於正確的配置管理。沒有配置基礎設施，無法進行任何功能開發。

**Independent Test**: 可透過以下方式獨立測試：
1. 在 appsettings.json 中新增配置區段
2. 建立對應的強型別配置類別
3. 在 Program.cs 中註冊配置至 DI 容器
4. 透過單元測試驗證配置可被正確注入並讀取

**Acceptance Scenarios**:

1. **Given** appsettings.json 中有新的配置區段，**When** 應用程式啟動，**Then** 配置類別應成功綁定到該區段的值
2. **Given** 配置類別已註冊到 DI 容器，**When** 任何服務請求該配置，**Then** 應返回正確的強型別實例
3. **Given** 必要的配置值未設定，**When** 應用程式啟動時驗證配置，**Then** 應拋出明確的錯誤訊息指出缺少哪個設定

---

### User Story 2 - 環境特定配置覆寫 (Priority: P2)

開發者需要能夠針對不同環境（Development、Production）提供不同的配置值，並在特定環境中覆寫基礎配置。

**Why this priority**: 雖然基礎配置已可運作，但多環境支援是實際部署的必要條件，優先級次於基礎功能。

**Independent Test**: 可透過以下方式獨立測試：
1. 建立 appsettings.Development.json 和 appsettings.Production.json
2. 在不同環境中覆寫特定配置值
3. 在對應環境中啟動應用程式
4. 驗證配置值是否正確反映環境特定的覆寫

**Acceptance Scenarios**:

1. **Given** appsettings.Development.json 中覆寫了某個配置值，**When** 在 Development 環境啟動應用程式，**Then** 應讀取到 Development 環境的配置值
2. **Given** appsettings.Production.json 中未覆寫某個配置值，**When** 在 Production 環境啟動應用程式，**Then** 應讀取到基礎 appsettings.json 的預設值

---

### User Story 3 - 敏感資訊透過環境變數注入 (Priority: P3)

開發者需要能夠透過環境變數注入敏感配置（如 API Token、連線字串），而不將這些資訊寫入 appsettings.json 檔案中。

**Why this priority**: 這是安全性最佳實踐，但可以在基礎配置運作後再實作，因此優先級較低。

**Independent Test**: 可透過以下方式獨立測試：
1. 設定環境變數（例如 `ReleaseKit__GitLab__Token`）
2. 啟動應用程式
3. 驗證配置類別中的對應屬性值來自環境變數而非 appsettings.json

**Acceptance Scenarios**:

1. **Given** 環境變數 `ReleaseKit__Section__Key` 已設定，**When** 應用程式啟動，**Then** 配置類別的 `Section:Key` 屬性應讀取到環境變數的值
2. **Given** 環境變數和 appsettings.json 都有設定同一個配置，**When** 應用程式啟動，**Then** 應優先採用環境變數的值

---

### Edge Cases

- 當配置區段名稱拼寫錯誤時，應用程式應在啟動時失敗並提供明確錯誤訊息
- 當必要配置值為空字串或 null 時，應在驗證階段拒絕
- 當配置類別屬性類型與 JSON 值不匹配時（例如期望 int 但提供 string），應提供類型轉換錯誤訊息
- 當環境變數格式不符合階層式命名規範（使用 `__` 分隔）時，應無法正確綁定

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: 系統必須能在 appsettings.json 中定義新的配置區段，並提供對應的強型別配置類別
- **FR-002**: 系統必須在應用程式啟動時將配置區段綁定到強型別類別，並註冊到 DI 容器中
- **FR-003**: 系統必須在應用程式啟動時驗證必要配置欄位是否已設定，若未設定應拋出 `InvalidOperationException` 並附帶明確錯誤訊息
- **FR-004**: 系統必須支援透過環境特定檔案（appsettings.{Environment}.json）覆寫基礎配置值
- **FR-005**: 系統必須支援透過環境變數注入配置值，環境變數應使用階層式命名（例如 `ReleaseKit__Section__Key` 對應 `ReleaseKit:Section:Key`）
- **FR-006**: 配置類別必須包含清晰的屬性名稱，並透過 XML 註解說明每個屬性的用途
- **FR-007**: 系統必須在配置綁定失敗時（例如類型不匹配）提供明確的錯誤訊息，指出哪個配置鍵值有問題

### Key Entities

- **配置區段 (Configuration Section)**: 代表 appsettings.json 中的一個邏輯群組，包含相關的配置鍵值對。每個區段對應一個強型別配置類別。
- **配置類別 (Configuration Class)**: 強型別的 C# 類別，其屬性對應到配置區段中的鍵值對，用於透過 DI 注入到需要使用配置的服務中。
- **配置驗證 (Configuration Validation)**: 在應用程式啟動時執行的驗證邏輯，確保必要的配置值已被設定且符合預期格式。

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 開發者可在 5 分鐘內完成新配置區段的新增、對應類別的建立，以及 DI 註冊
- **SC-002**: 配置驗證機制可在應用程式啟動的 1 秒內偵測出缺少的必要配置，並拋出明確錯誤
- **SC-003**: 100% 的必要配置欄位都有對應的驗證邏輯，確保應用程式不會在缺少配置時執行
- **SC-004**: 所有配置類別屬性都有 XML 註解，讓開發者在使用時能快速理解其用途
- **SC-005**: 環境變數可成功覆寫 appsettings.json 中的配置值，驗證優先順序正確
