<!--
================================================================================
Sync Impact Report
================================================================================
Version change: 1.1.0 → 1.2.0 (MINOR - 新增原則與規範強化)
Modified principles:
  - VI. 效能與快取優先 → 擴充為涵蓋資料存取最佳實踐
  - IX. JSON 序列化規範 → 新增 JsonExtensions 優先順序
Added sections:
  - XII. RESTful API 規範（API 路徑設計與快取策略）
  - XIII. 組態管理規範（domain/設定檔管理原則）
  - XIV. 程式碼重用原則（Plan 與 Task 指令優先搜尋現有邏輯）
Removed sections: N/A
Templates requiring updates:
  - .specify/templates/plan-template.md ✅ 已檢視，與原則 XIV 一致
  - .specify/templates/spec-template.md ✅ 已檢視，無需更新
  - .specify/templates/tasks-template.md ✅ 已檢視，與原則 XIV 一致
Follow-up TODOs: 無
================================================================================
-->

# Release-Kit Constitution

## Core Principles

### I. 測試驅動開發 (TDD - 不可妥協)

TDD 為強制性開發流程，所有功能實作 MUST 遵循 Red-Green-Refactor 循環：

- 先撰寫測試 → 確認測試失敗 → 實作功能 → 確認測試通過 → 重構
- 單元測試 MUST 涵蓋所有業務邏輯
- 整合測試 MUST 涵蓋跨服務通訊與資料契約變更

**理由**: 確保程式碼品質與可維護性，減少回歸錯誤。

### II. 領域驅動設計 (DDD) 與 CQRS

專案架構 MUST 遵循 DDD 與 CQRS 原則：

- 領域模型 MUST 封裝業務邏輯，禁止貧血模型 (Anemic Domain Model)
- Command 與 Query 職責分離，讀寫操作使用不同的模型
- Aggregate Root 負責維護領域不變條件
- Repository Pattern 用於資料存取抽象

**理由**: 提升程式碼可讀性與業務邏輯表達力，便於團隊協作。

### III. SOLID 原則

所有類別與模組設計 MUST 遵循 SOLID 原則：

- **S**ingle Responsibility: 一個類別只負責一項職責
- **O**pen-Closed: 對擴展開放，對修改封閉
- **L**iskov Substitution: 子類別可替換父類別
- **I**nterface Segregation: 介面最小化，避免肥大介面
- **D**ependency Inversion: 依賴抽象而非實作

**理由**: 提升程式碼的可維護性、可測試性與可擴展性。

### IV. 簡單原則 (KISS)

實作 MUST 保持簡單，避免過度設計：

- 優先選擇直觀、易讀的解決方案
- 避免不必要的抽象層級
- 只解決當前問題，不預先設計未來需求 (YAGNI)
- 程式碼複雜度增加 MUST 有明確理由與文件記錄

**理由**: 降低維護成本，提升團隊開發效率。

### V. 錯誤處理策略

專案 MUST 使用結構化錯誤處理，禁止使用 try-catch：

- 使用 Result Pattern 或類似的錯誤回傳機制
- 錯誤類型 MUST 明確定義且具有語意
- 錯誤訊息 MUST 包含足夠的診斷資訊
- 禁止吞掉例外或使用空的 catch 區塊

**理由**: 強制開發者明確處理所有可能的錯誤情境，提升系統穩定性。

### VI. 效能與快取優先

資料存取 MUST 遵循效能優先原則：

- 撈取資料前 MUST 確認是否有現成邏輯可重複使用
- 避免直接使用 DbContext 撈取資料，MUST 優先使用既有的 Repository 或 Service 方法
- 如有快取機制可用，MUST 優先使用快取取得資料
- 避免 N+1 查詢問題
- 資料庫查詢 MUST 有適當的索引支援

**理由**: 確保系統效能，提升使用者體驗，並維持資料存取邏輯的一致性。

### VII. 避免硬編碼

所有固定值 MUST 使用 Enum 或 Constant 管理：

- 魔術數字 (Magic Number) 禁止出現在程式碼中
- 設定值 MUST 透過組態檔或環境變數管理
- 字串常數 MUST 集中管理

**理由**: 提升程式碼可維護性，便於統一修改。

### VIII. 文件與註解規範

程式碼 MUST 包含適當的文件與註解：

- 所有公開類別與方法 MUST 加入 summary XML 註解
- 複雜邏輯 MUST 加入適量的 inline comment 說明
- 註解 MUST 使用繁體中文 (zh-tw)
- 避免過度註解，程式碼應具自我解釋能力

**理由**: 提升程式碼可讀性，便於團隊協作與知識傳承。

### IX. JSON 序列化規範

JSON 處理 MUST 遵循以下優先順序：

1. 優先使用 JsonExtensions（如專案已存在）
2. 不存在時使用 System.Text.Json
3. 禁止使用 Newtonsoft.Json（除非有明確的相容性需求）

**理由**: 統一序列化行為，避免不一致的 JSON 處理邏輯。

### X. 程式進入點規範

program.cs MUST 保持整潔：

- 只負責應用程式啟動與 DI 容器設定
- 業務邏輯禁止寫在 program.cs
- 中介軟體設定 SHOULD 抽取至獨立的擴充方法
- 服務註冊 SHOULD 按功能模組分組

**理由**: 維持進入點的單一職責，便於理解應用程式啟動流程。

### XI. 檔案組織規範

程式碼檔案 MUST 遵守單一類別原則：

- 一個檔案只能包含一個類別 (One Class Per File)
- 檔案名稱 MUST 與類別名稱完全一致
- 巢狀類別 (Nested Class) 為例外，可與父類別放在同一檔案
- 禁止將多個公開類別放在同一檔案中

**理由**: 提升程式碼可搜尋性與可維護性，便於快速定位類別定義。

### XII. RESTful API 規範

API 設計 MUST 遵循 RESTful 原則：

- 新增 API 路徑 MUST 遵循 RESTful API 規範
  - 使用標準 HTTP 方法：GET（查詢）、POST（新增）、PUT/PATCH（更新）、DELETE（刪除）
  - 路徑使用名詞複數表達資源（如 `/api/workitems`）
  - 使用階層結構表達關聯（如 `/api/workitems/{id}/comments`）
- GET 方法 MUST 優先使用 `[ResponseCache]` 屬性禁用快取或設定適當的快取策略
  - 預設禁用快取：`[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]`
  - 如需快取則明確設定時間與位置
- 狀態碼 MUST 語意正確：200（成功）、201（已建立）、204（無內容）、400（錯誤請求）、404（未找到）、500（伺服器錯誤）

**理由**: 統一 API 設計風格，提升 API 可預測性與可維護性，避免快取導致的資料不一致問題。

### XIII. 組態管理規範

組態管理 MUST 遵循集中化原則：

- 所有 domain、URL、連線字串等設定 MUST 設定至 `appsettings.json` 或 `settings.json`
- 根據專案內容選擇適當的組態檔：
  - ASP.NET Core 專案使用 `appsettings.json`
  - Console 或通用專案使用 `settings.json` 或 `appsettings.json`
- 敏感資訊（API Token、密碼）MUST 透過環境變數或 Secret Manager 管理
- 組態鍵值 MUST 使用階層式命名（如 `Redis:ConnectionString`）
- 禁止在程式碼中硬編碼環境相關的設定值

**理由**: 便於環境切換（開發、測試、正式），提升部署彈性，避免敏感資訊洩漏。

### XIV. 程式碼重用原則

開發流程 MUST 優先重用現有程式碼：

- **Plan 指令**：規劃新功能時 MUST 優先搜尋並重複使用現有的邏輯與元件
  - 使用 grep/glob 工具搜尋相似功能
  - 記錄可重用的元件清單於計畫文件中
  - 避免重複造輪子
- **Task 指令**：執行任務時 MUST 標註階段性任務的建置與測試狀態
  - 程式是否可以正確建置：✅ 可建置 / ❌ 無法建置
  - 單元測試是否會通過：✅ 通過 / ❌ 失敗 / ⚠️ 部分通過
  - 每個階段性任務完成後 MUST 執行驗證

**理由**: 提升開發效率，維持程式碼一致性，減少維護成本，確保每個開發階段的品質可控。

## 程式碼風格規範

### C# 設計模式應用

- 使用 Factory Pattern 建立複雜物件
- 使用 Strategy Pattern 處理可替換的演算法
- 使用 Decorator Pattern 擴充功能而非修改原有程式碼
- 使用 Mediator Pattern (如 MediatR) 處理 CQRS Command/Query

### 命名慣例

- 類別名稱使用 PascalCase
- 方法名稱使用 PascalCase
- 私有欄位使用 _camelCase
- 參數與區域變數使用 camelCase
- 介面名稱以 I 開頭 (如 IRepository)

## 開發工作流程

### Plan 指令規範

執行 Plan 指令時 MUST：

- 優先搜尋現有程式碼庫中的相關邏輯
- 優先重複使用現有元件，避免重複造輪子
- 記錄可重用的元件清單於計畫文件中

### Task 指令規範

執行 Task 指令時 MUST：

- 每個階段性任務 MUST 標註：
  - 程式是否可以正確建置 (✅ 可建置 / ❌ 無法建置)
  - 單元測試是否會通過 (✅ 通過 / ❌ 失敗 / ⚠️ 部分通過)
- 任務完成後 MUST 執行建置驗證

## Governance

### 規範優先權

本 Constitution 優先於所有其他開發實踐。發生衝突時，以本文件為準。

### 修訂程序

Constitution 修訂 MUST 遵循以下程序：

1. 提出修訂建議並說明理由
2. 記錄修訂內容與影響範圍
3. 更新版本號（依語意化版本規則）
4. 同步更新相關模板與文件

### 合規檢查

- 所有 PR/Code Review MUST 驗證是否符合 Constitution
- 新增複雜度 MUST 有明確理由記錄於 Complexity Tracking
- 使用 CLAUDE.md 作為執行階段開發指引

### 語言規範

- 所有溝通與文件 MUST 使用繁體中文 (zh-tw)
- 技術術語與程式碼識別符保持原文
- 註解與文件 MUST 使用繁體中文

**Version**: 1.2.0 | **Ratified**: 2026-01-27 | **Last Amended**: 2026-02-14
