# Quickstart: Redis → Google Sheet 批次同步

**Feature**: 001-redis-sheet-sync
**Date**: 2026-02-24

## 前置條件

1. Redis 中已有 `ReleaseData:Consolidated` 資料（由 `ConsolidateReleaseData` 任務產生）
2. Google Sheet 已建立，且有正確的工作表名稱
3. Google Service Account 已建立，且有工作表的編輯權限
4. `appsettings.json` 已設定 `GoogleSheet` 區段

## 設定範例

```json
{
  "GoogleSheet": {
    "SpreadsheetId": "your-spreadsheet-id",
    "SheetName": "Release Notes",
    "ServiceAccountCredentialPath": "/path/to/service-account.json",
    "ColumnMapping": {
      "RepositoryNameColumn": "Z",
      "FeatureColumn": "B",
      "TeamColumn": "D",
      "AuthorsColumn": "E",
      "PullRequestUrlsColumn": "X",
      "UniqueKeyColumn": "Y",
      "AutoSyncColumn": "F"
    }
  }
}
```

## 執行方式

```bash
# 先執行整合任務（產生 Redis 資料）
dotnet run --project src/ReleaseKit.Console -- consolidate-release-data

# 再執行 Google Sheet 同步
dotnet run --project src/ReleaseKit.Console -- update-google-sheets
```

## 行為說明

### 正常流程

1. 從 Redis 讀取 `ReleaseData:Consolidated` 整合資料
2. 驗證 Google Sheet 可連線，取得 Sheet ID 與現有資料（A–Z）
3. 比對 UniqueKey 欄位，分類為「新增」或「更新」
4. 批次插入所有需要的空白列
5. 填入新增資料（含超連結）並更新既有資料
6. 對有變動的專案區段執行排序

### 無資料情境

- Redis 無整合資料 → 記錄日誌後正常結束
- Google Sheet 無法連線 → 記錄日誌後正常結束
- 所有資料皆已存在且無變更 → 僅更新 Authors/PR URLs 欄位

### 錯誤情境

- 欄位對應超過 Z 欄 → 拋出錯誤並終止

## 開發與測試

```bash
# 建置
dotnet build src/release-kit.sln

# 執行所有測試
dotnet test src/release-kit.sln

# 僅執行 Application 層測試
dotnet test tests/ReleaseKit.Application.Tests/
```

## 新增的 NuGet 套件

| 套件 | 專案 | 用途 |
|------|------|------|
| Google.Apis.Sheets.v4 | ReleaseKit.Infrastructure | Google Sheets API 存取 |
| Google.Apis.Auth | ReleaseKit.Infrastructure | Service Account 認證 |
