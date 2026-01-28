# Configuration Contracts

本目錄包含設定檔的契約定義，用於驗證與 IDE 支援。

## 檔案說明

### appsettings-schema.json
JSON Schema 定義，用於：
- 驗證 appsettings.json 的結構
- 提供 IDE IntelliSense 支援（需配置）
- 文件化必填欄位與格式要求

### example-appsettings.annotated.json
完整的設定檔範例，包含註解說明每個欄位的用途。

---

## 如何在 IDE 中啟用 IntelliSense

### Visual Studio Code

在 `appsettings.json` 的第一行加入：

```json
{
  "$schema": "../specs/001-appsettings-configuration/contracts/appsettings-schema.json",
  "Serilog": {
    // ...
  }
}
```

### Visual Studio 2022

1. 工具 → 選項 → 文字編輯器 → JSON → 結構描述
2. 新增對應：
   - **檔案模式**: `**/appsettings*.json`
   - **結構描述 URI**: 專案相對路徑

### JetBrains Rider

1. Settings → Languages & Frameworks → Schemas and DTDs → JSON Schema Mappings
2. 新增對應：
   - **Name**: ReleaseKit Settings
   - **Schema file or URL**: 選擇 `appsettings-schema.json`
   - **File path pattern**: `appsettings*.json`

---

## 設定驗證

### 命令列驗證（需安裝 ajv-cli）

```bash
# 安裝驗證工具
npm install -g ajv-cli

# 驗證設定檔
ajv validate \
  -s specs/001-appsettings-configuration/contracts/appsettings-schema.json \
  -d src/ReleaseKit.Console/appsettings.json
```

### CI/CD 整合範例

```yaml
# .github/workflows/validate-config.yml
name: Validate Configuration

on:
  pull_request:
    paths:
      - 'src/ReleaseKit.Console/appsettings*.json'

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-node@v3
      - run: npm install -g ajv-cli
      - run: |
          ajv validate \
            -s specs/001-appsettings-configuration/contracts/appsettings-schema.json \
            -d src/ReleaseKit.Console/appsettings*.json
```

---

## 維護說明

### 何時更新 Schema？

- ✅ 新增 Options 類別時
- ✅ 修改必填欄位時
- ✅ 變更欄位格式或驗證規則時

### Schema 更新流程

1. 修改 `appsettings-schema.json`
2. 更新 `example-appsettings.annotated.json` 範例
3. 在 PR 中註明 Schema 變更
4. 確保向後相容或提供遷移指引

---

## 版本歷史

| 版本 | 日期 | 變更內容 |
|------|------|---------|
| 1.0 | 2025-01-28 | 初始 Schema 定義（基於現有設定結構） |
