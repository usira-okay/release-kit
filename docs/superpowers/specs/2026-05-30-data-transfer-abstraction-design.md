# 資料傳遞抽象化設計文件

**日期：** 2026-05-30  
**狀態：** 已批准，待實作

## 背景與目標

目前各 Task 之間傳遞資料的唯一方式是透過 Redis（`IRedisService`）。此設計目標是：

1. 將資料傳遞機制抽象化，消除對 Redis 的硬性依賴
2. 新增 FileSystem（本地檔案）作為替代後端
3. 透過 `appsettings.json` 的 `DataTransfer:Provider` 設定切換後端
4. 同步更新所有 Redis 相關命名（類別、變數、屬性、註解），使程式語義更清晰

## 架構概覽

```
Domain.Abstractions/
  IDataTransferService        ← 新介面（取代 IRedisService）

Infrastructure/DataTransfer/
  Redis/
    RedisDataTransferService  ← 原 RedisService 改名並搬移
  FileSystem/
    FileSystemDataTransferService ← 新實作

Common/Constants/
  DataTransferKeys            ← 原 RedisKeys 改名

Console/Extensions/
  ServiceCollectionExtensions
    AddDataTransferServices() ← 原 AddRedisServices()，依 Provider 切換注入
```

## 介面設計

### `IDataTransferService`

位置：`ReleaseKit.Domain/Abstractions/IDataTransferService.cs`

```csharp
// Key-Value 操作
Task<bool> SetAsync(string key, string value, TimeSpan? expiry = null);
Task<string?> GetAsync(string key);
Task<bool> DeleteAsync(string key);
Task<bool> ExistsAsync(string key);

// Group 操作（原 Hash 操作，以中性命名取代 Redis 術語）
Task<bool> GroupSetAsync(string groupKey, string field, string value);
Task<string?> GroupGetAsync(string groupKey, string field);
Task<bool> GroupDeleteAsync(string groupKey, string field);
Task<bool> GroupExistsAsync(string groupKey, string field);
Task<IReadOnlyDictionary<string, string>> GroupGetAllAsync(string groupKey);
```

舊介面 `IRedisService` 在新介面穩定後刪除。

## 實作設計

### `RedisDataTransferService`

- 位置：`ReleaseKit.Infrastructure/DataTransfer/Redis/RedisDataTransferService.cs`
- 從舊 `Redis/RedisService.cs` 搬移、改名
- 實作不變，只是方法名稱從 `Hash*` 改為 `Group*`，內部仍呼叫 StackExchange.Redis 的 `HashSet`/`HashGet` 等

### `FileSystemDataTransferService`

- 位置：`ReleaseKit.Infrastructure/DataTransfer/FileSystem/FileSystemDataTransferService.cs`
- 建構子注入 `string fileDirectory`（來自設定）

**路徑對應：**

| 操作 | 本地路徑 |
|------|---------|
| `SetAsync(key, value)` | `{fileDirectory}/{key}` |
| `GetAsync(key)` | `{fileDirectory}/{key}` |
| `DeleteAsync(key)` | 刪除 `{fileDirectory}/{key}` |
| `ExistsAsync(key)` | 檢查 `{fileDirectory}/{key}` 是否存在 |
| `GroupSetAsync(groupKey, field, value)` | `{fileDirectory}/{groupKey}/{field}` |
| `GroupGetAsync(groupKey, field)` | `{fileDirectory}/{groupKey}/{field}` |
| `GroupDeleteAsync(groupKey, field)` | 刪除 `{fileDirectory}/{groupKey}/{field}` |
| `GroupExistsAsync(groupKey, field)` | 檢查 `{fileDirectory}/{groupKey}/{field}` 是否存在 |
| `GroupGetAllAsync(groupKey)` | 列舉 `{fileDirectory}/{groupKey}/` 目錄內所有檔案 |

**注意事項：**
- 目錄不存在時自動建立（`Directory.CreateDirectory`）
- 本工具為 CLI 單一 Process，不需處理並發鎖定
- 檔案不存在時 `GetAsync`/`GroupGetAsync` 回傳 `null`，`ExistsAsync`/`GroupExistsAsync` 回傳 `false`
- `SetAsync` 的 `expiry` 參數在 FileSystem 實作中忽略（CLI 工具不需要 TTL）

## 設定檔變更

### `appsettings.json` 新增

```json
"DataTransfer": {
  "Provider": "FileSystem",
  "FileDirectory": "/tmp/release-kit"
}
```

支援的 `Provider` 值：`Redis`、`FileSystem`

`Redis` 區段保持不變（`Provider = Redis` 時才使用）：

```json
"Redis": {
  "ConnectionString": "localhost:6379",
  "InstanceName": "ReleaseKit:"
}
```

## DI 注冊

`ServiceCollectionExtensions.AddRedisServices` → `AddDataTransferServices`

```csharp
public static IServiceCollection AddDataTransferServices(
    this IServiceCollection services, IConfiguration configuration)
{
    var provider = configuration["DataTransfer:Provider"]
        ?? throw new InvalidOperationException("DataTransfer:Provider 組態設定不得為空");

    return provider switch
    {
        "Redis"      => services.AddRedisDataTransfer(configuration),
        "FileSystem" => services.AddFileSystemDataTransfer(configuration),
        _            => throw new InvalidOperationException($"不支援的資料傳遞方式: {provider}")
    };
}
```

## 命名重構對照表

| 舊名 | 新名 | 位置 |
|------|------|------|
| `IRedisService` | `IDataTransferService` | `Domain/Abstractions` |
| `RedisService` | `RedisDataTransferService` | `Infrastructure/DataTransfer/Redis` |
| `RedisKeys` | `DataTransferKeys` | `Common/Constants` |
| `_redisService` | `_dataTransferService` | 所有 Task 類別 |
| `IRedisService redisService` (參數) | `IDataTransferService dataTransferService` | 所有 Task 建構子 |
| `RedisHashKey` (抽象屬性) | `DataTransferGroupKey` | `BaseFetchPullRequestsTask`, `BaseFetchReleaseBranchTask` |
| `RedisHashField` (抽象屬性) | `DataTransferGroupField` | 同上 |
| `SourceRedisHashKey` | `SourceGroupKey` | `BaseFilterPullRequestsByUserTask` |
| `SourceRedisHashField` | `SourceGroupField` | 同上 |
| `TargetRedisHashKey` | `TargetGroupKey` | 同上 |
| `TargetRedisHashField` | `TargetGroupField` | 同上 |
| `HashSet*` / `HashGet*` / `HashDelete*` / `HashExists*` 方法呼叫 | `GroupSet*` / `GroupGet*` / `GroupDelete*` / `GroupExists*` | 所有使用 Hash 操作的 Task |
| `AddRedisServices` | `AddDataTransferServices` | `ServiceCollectionExtensions` |

## 測試策略

### 更新現有測試
- 所有 mock `IRedisService` 改為 mock `IDataTransferService`
- 更新 `Hash*` 呼叫為 `Group*`
- `RedisServiceTests` → `RedisDataTransferServiceTests`

### 新增測試
- `FileSystemDataTransferServiceTests`：
  - Group 操作讀寫（Set/Get/Delete/Exists）
  - Plain Key 操作讀寫
  - `GroupGetAllAsync` 正確列舉目錄內所有檔案
  - 目錄不存在時自動建立
  - 不存在時回傳 `null` / `false` 的邊界行為

## 影響範圍

以下檔案需修改：
- `ReleaseKit.Domain/Abstractions/IRedisService.cs` → 刪除，改為 `IDataTransferService.cs`
- `ReleaseKit.Infrastructure/Redis/RedisService.cs` → 搬移至 `DataTransfer/Redis/RedisDataTransferService.cs`
- 新建 `ReleaseKit.Infrastructure/DataTransfer/FileSystem/FileSystemDataTransferService.cs`
- `ReleaseKit.Common/Constants/RedisKeys.cs` → 改名為 `DataTransferKeys.cs`
- `ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs`
- `ReleaseKit.Console/appsettings.json`
- `ReleaseKit.Application/Tasks/` 下所有使用 `IRedisService` 的 Task（共 11 個類別）
- `tests/ReleaseKit.Application.Tests/Tasks/` 下所有相關測試
- `tests/ReleaseKit.Infrastructure.Tests/Redis/RedisServiceTests.cs` → 改名更新
