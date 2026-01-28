# Implementation Plan: Configuration Settings Infrastructure

**Branch**: `001-appsettings-configuration` | **Date**: 2025-01-28 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-appsettings-configuration/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

本功能為 meta-feature，旨在建立標準化的設定基礎架構模式與文件。專案已具備完善的 Options Pattern 實作（GitLabOptions、BitbucketOptions 等），本計畫將文件化現有最佳實踐，並補充驗證模式與快速入門指南，確保未來功能開發能一致地遵循相同的設定管理模式。

## Technical Context

**Language/Version**: C# / .NET 9.0  
**Primary Dependencies**: 
  - Microsoft.Extensions.Configuration (9.0.1)
  - Microsoft.Extensions.Configuration.Json (9.0.1)
  - Microsoft.Extensions.Options (9.0.1 - 隱含依賴)
  - Microsoft.Extensions.DependencyInjection (9.0.1)
**Storage**: N/A（設定檔基礎架構，無資料持久化需求）  
**Testing**: xUnit (2.9.2) + Microsoft.NET.Test.Sdk (17.12.0)  
**Target Platform**: Console Application (跨平台 .NET 9.0)  
**Project Type**: Console Application with Clean Architecture (4-layer: Domain, Application, Infrastructure, Console)  
**Performance Goals**: 設定載入與綁定須於應用程式啟動時完成（<100ms），無運行時效能影響  
**Constraints**: 
  - 設定類別必須位於 Console 層（`/src/ReleaseKit.Console/Options/`）
  - 遵循 Options Pattern（IOptions<T>）而非直接存取 IConfiguration
  - 支援多環境設定（Development, Production, QA, Docker）
  - 設定檔格式限定為 JSON
**Scale/Scope**: 
  - 預計支援 10-20 個設定區段
  - 每個 Options 類別約 5-15 個屬性
  - 支援巢狀設定物件與集合

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Gate 1: 測試驅動開發 (TDD) ✅ PASS
- **評估**: 本 meta-feature 主要產出為文件與範例程式碼，無新的業務邏輯需實作
- **行動**: Phase 2 若生成範例程式碼，須包含對應的單元測試示範

### Gate 2: 領域驅動設計 (DDD) & CQRS ⚠️ ADAPTED
- **評估**: 設定基礎架構屬於技術橫切關注點，不涉及領域模型
- **行動**: Options 類別為 POCO，不包含業務邏輯，符合關注點分離原則

### Gate 3: SOLID 原則 ✅ PASS
- **評估**: 
  - SRP: 每個 Options 類別只負責一個設定區段
  - OCP: 透過擴充方法模式開放擴展
  - DIP: 依賴 IOptions<T> 抽象而非具體實作
- **行動**: 確保文件強調 Options 類別的單一職責原則

### Gate 4: 簡單原則 (KISS) ✅ PASS
- **評估**: Options Pattern 為 .NET 標準模式，無過度設計
- **行動**: 文件應避免引入不必要的驗證框架或複雜抽象

### Gate 5: 錯誤處理策略 ⚠️ PARTIAL
- **評估**: 設定載入錯誤由 ASP.NET Core 框架處理，會拋出例外
- **行動**: 文件需說明設定驗證的最佳實踐（使用 IValidateOptions<T> 或啟動時驗證）

### Gate 6: 效能與快取優先 ✅ PASS
- **評估**: IOptions<T> 為 Singleton，IOptionsSnapshot<T> 為 Scoped，無效能問題
- **行動**: 文件需說明三種 Options 介面的使用時機

### Gate 7: 避免硬編碼 ✅ PASS
- **評估**: 此為設定管理基礎架構，直接服務於此目標
- **行動**: 文件需強調所有環境特定值須透過設定檔管理

### Gate 8: 文件與註解規範 ✅ PASS
- **評估**: 現有 Options 類別已包含完整的 XML 註解（繁體中文）
- **行動**: 確保新增的範例程式碼與文件維持相同品質

### Gate 9: JSON 序列化規範 ⚠️ PARTIAL
- **評估**: Options Pattern 使用 System.Text.Json 進行設定綁定
- **行動**: 文件需說明設定檔與 Options 類別的命名對應規則

### Gate 10: 程式進入點規範 ✅ PASS
- **評估**: Program.cs 已使用 `AddConfigurationOptions` 擴充方法保持整潔
- **行動**: 文件需強調此模式，避免在 Program.cs 直接註冊設定

### Gate 11: 檔案組織規範 ✅ PASS
- **評估**: 每個 Options 類別已獨立於單一檔案
- **行動**: 文件需明確此規範，確保未來遵循

**總結**: 11 項檢查中 7 項完全通過，4 項需調整（已規劃對應行動），無違反項目。

## Project Structure

### Documentation (this feature)

```text
specs/001-appsettings-configuration/
├── plan.md              # 本檔案 (/speckit.plan 指令產出)
├── research.md          # Phase 0 產出：Options Pattern 最佳實踐研究
├── data-model.md        # Phase 1 產出：Options 類別結構模型
├── quickstart.md        # Phase 1 產出：快速入門指南
├── contracts/           # Phase 1 產出：設定檔 JSON Schema（若適用）
└── tasks.md             # Phase 2 產出（/speckit.tasks 指令 - 本計畫不生成）
```

### Source Code (repository root)

```text
src/
├── ReleaseKit.Domain/              # 領域層（本 feature 不涉及）
├── ReleaseKit.Application/         # 應用層（本 feature 不涉及）
├── ReleaseKit.Infrastructure/      # 基礎設施層（本 feature 不涉及）
└── ReleaseKit.Console/             # 展示層（設定類別位置）
    ├── Options/                    # 📌 設定類別目錄（本 feature 核心）
    │   ├── GitLabOptions.cs            # 現有範例
    │   ├── GitLabProjectOptions.cs     # 現有範例（巢狀物件）
    │   ├── BitbucketOptions.cs         # 現有範例
    │   ├── UserMappingOptions.cs       # 現有範例
    │   └── [未來新增的 Options 類別]
    ├── Extensions/
    │   └── ServiceCollectionExtensions.cs  # 📌 DI 註冊集中處理
    ├── Program.cs                  # 應用程式進入點
    ├── appsettings.json            # 📌 基礎設定檔
    ├── appsettings.Development.json    # 開發環境覆寫
    ├── appsettings.Production.json     # 生產環境覆寫
    ├── appsettings.Qa.json             # QA 環境覆寫
    └── ReleaseKit.Console.csproj

tests/
├── ReleaseKit.Console.Tests/       # 📌 設定類別測試位置
│   └── Options/                    # Options 測試目錄
├── ReleaseKit.Application.Tests/
├── ReleaseKit.Domain.Tests/
└── ReleaseKit.Infrastructure.Tests/
```

**Structure Decision**: 
專案採用 Clean Architecture 的 4 層架構，設定類別位於最外層的 Console（展示層）。此決策符合關注點分離原則：
- 設定為基礎建設的技術細節，不應污染內層（Domain/Application）
- Options 類別作為 DTO 綁定外部設定至應用程式
- ServiceCollectionExtensions 集中管理 DI 註冊，避免 Program.cs 膨脹

## Complexity Tracking

> **本 feature 無 Constitution 違反項目，本節為空**

本 meta-feature 完全符合專案憲章的所有規範，無需額外的複雜度追蹤。


---

# Phase 0: Outline & Research

## Research Objectives

本階段目標為研究並文件化以下領域的最佳實踐：

### RO-1: Options Pattern 實作模式
**問題**: 如何在 .NET 9.0 中正確實作 Options Pattern？
**研究範圍**:
- IOptions<T> vs IOptionsSnapshot<T> vs IOptionsMonitor<T> 的差異與使用時機
- Options 類別的設計原則（POCO、預設值、可空性）
- 設定綁定的命名對應規則（PascalCase vs camelCase）
- 巢狀物件與集合的綁定方式

### RO-2: 設定驗證策略
**問題**: 如何確保設定值在應用程式啟動時是有效的？
**研究範圍**:
- Data Annotations 驗證 vs IValidateOptions<T>
- 啟動時驗證 vs 延遲驗證的權衡
- 必填欄位的處理方式（非空類型 vs 預設值 vs 驗證）
- 驗證錯誤訊息的最佳實踐

### RO-3: 環境特定設定管理
**問題**: 如何有效管理多環境設定並避免敏感資訊洩漏？
**研究範圍**:
- appsettings.{Environment}.json 的合併邏輯
- User Secrets 的使用時機與限制
- 環境變數覆寫的命名規則
- Docker 環境的設定策略

### RO-4: DI 註冊的組織模式
**問題**: 如何組織大量的設定註冊以維持 Program.cs 的整潔？
**研究範圍**:
- 擴充方法的分組策略（功能別 vs 層級別）
- Configure<T> 的效能考量（Singleton 註冊）
- 條件式設定註冊（基於環境或 Feature Flag）

## Phase 0 Deliverable

預期產出 `research.md`，包含：
1. 每個研究目標的決策建議
2. 現有專案程式碼範例的分析
3. 微軟官方文件的最佳實踐總結
4. 各種設定模式的權衡分析


---

# Phase 1: Design & Contracts

## Prerequisites
- `research.md` 已完成並經審查

## Design Objectives

### DO-1: 定義 Options 類別標準結構
**產出**: `data-model.md`  
**內容**:
```markdown
# Options 類別設計模型

## 基本結構範本
- 命名規範：{功能}Options
- 屬性命名：PascalCase（對應 JSON 的 camelCase 或 PascalCase）
- 預設值策略：string.Empty vs null vs 具體值
- XML 註解要求：每個屬性必須有 <summary>

## 複雜結構模式
- 巢狀物件：何時拆分為獨立 Options 類別
- 集合屬性：List<T> vs IReadOnlyList<T>
- 字典屬性：Dictionary<string, T> 的綁定方式

## 實體範例
（基於現有的 GitLabOptions、BitbucketOptions 擷取模式）
```

### DO-2: 生成設定檔結構契約
**產出**: `contracts/appsettings-schema.json`（選用）  
**內容**:
- JSON Schema 定義（若團隊需要 IDE IntelliSense 支援）
- 或改以範例設定檔 + 註解的形式呈現

### DO-3: 撰寫快速入門指南
**產出**: `quickstart.md`  
**內容**:
```markdown
# 設定管理快速入門

## 情境 1: 新增簡單設定區段
1. 建立 Options 類別
2. 更新 appsettings.json
3. 註冊至 DI 容器
4. 注入至服務使用

## 情境 2: 新增包含集合的設定
（類似 GitLabOptions.Projects 的模式）

## 情境 3: 新增環境特定覆寫
（Development 使用 localhost, Production 使用實際網址）

## 情境 4: 設定驗證（選用）
（基於 Phase 0 研究的建議方式）
```

### DO-4: 更新 Agent Context
**動作**: 執行 `.specify/scripts/bash/update-agent-context.sh copilot`  
**目的**: 
- 將本 feature 的設定模式加入 AI Agent 的上下文
- 確保未來開發時 AI 能建議正確的設定模式

## Phase 1 Deliverable Checklist
- [ ] `data-model.md` - Options 類別設計規範
- [ ] `quickstart.md` - 開發者快速入門指南
- [ ] `contracts/` - 設定檔契約（JSON Schema 或範例）
- [ ] Agent context 已更新
- [ ] Constitution Check 複審通過


---

# Phase 2: Tasks Generation (Out of Scope)

**注意**: 本 meta-feature 的 Phase 2 不會生成 `tasks.md`。

**理由**: 
- 本 feature 為文件與模式建立，無實際程式碼變更需求
- 現有的 Options Pattern 實作已完備，無需重構
- 若未來需要補充驗證邏輯或範例程式碼，可作為獨立的 follow-up feature


---

# Implementation Notes

## Reusable Components Identified

專案中已存在完善的設定基礎架構，可直接參考：

1. **GitLabOptions.cs** - 展示巢狀物件與集合的使用
2. **ServiceCollectionExtensions.AddConfigurationOptions()** - 集中式 DI 註冊模式
3. **Program.cs (Lines 20-29)** - 標準的設定載入層次結構
4. **appsettings.json** - 多層級設定範例

## Risk Assessment

| 風險 | 機率 | 影響 | 緩解措施 |
|------|------|------|---------|
| 文件與實際程式碼不一致 | 中 | 中 | 從現有程式碼擷取範例，確保一致性 |
| 驗證模式建議過於複雜 | 低 | 中 | 遵循 KISS 原則，優先使用簡單方案 |
| 未來開發者不遵循文件 | 中 | 低 | 透過 Code Review 與 Agent Context 強化 |

## Success Metrics

- ✅ 新成員能在 10 分鐘內根據 quickstart.md 新增設定
- ✅ 所有新設定遵循文件化的命名與結構規範
- ✅ Zero 設定載入相關的執行時錯誤（透過啟動驗證）
