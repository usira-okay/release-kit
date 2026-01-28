# Research Report: Options Pattern 最佳實踐

**Feature**: Configuration Settings Infrastructure  
**Date**: 2025-01-28  
**Status**: Completed

本研究報告針對 .NET 9.0 的 Options Pattern 最佳實踐進行深入分析，並基於專案現有的設定架構提出標準化建議。

---

## RO-1: Options Pattern 實作模式

### Decision: IOptions<T> vs IOptionsSnapshot<T> vs IOptionsMonitor<T>

| 介面類型 | 生命週期 | 重新載入支援 | 使用時機 |
|---------|---------|------------|---------|
| **IOptions<T>** | Singleton | ❌ 否 | 應用程式啟動後不變的設定（如 API URLs, 連線字串） |
| **IOptionsSnapshot<T>** | Scoped | ✅ 是（每個 Request） | 需要在每個請求中重新讀取的設定 |
| **IOptionsMonitor<T>** | Singleton | ✅ 是（即時變更通知） | 需要監聽設定變更並即時反應的場景 |

**專案建議**: 
- **預設使用 IOptions<T>** - 專案為 Console Application，設定於啟動時載入即可
- GitLabOptions、BitbucketOptions 等皆適用 IOptions<T>
- 若未來需要熱重載功能，可針對特定設定升級為 IOptionsMonitor<T>

**現有程式碼範例** (ServiceCollectionExtensions.cs:58-64):
```csharp
services.Configure<GitLabOptions>(configuration.GetSection("GitLab"));
services.Configure<BitbucketOptions>(configuration.GetSection("Bitbucket"));
services.Configure<UserMappingOptions>(configuration.GetSection("UserMapping"));
```

### Decision: Options 類別設計原則

#### 1. POCO 原則
Options 類別必須為純資料容器，不包含業務邏輯：

✅ **正確範例** (GitLabOptions.cs):
```csharp
public class GitLabOptions
{
    public string ApiUrl { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public List<GitLabProjectOptions> Projects { get; set; } = new();
}
```

❌ **錯誤範例**:
```csharp
public class GitLabOptions
{
    public string ApiUrl { get; set; } = string.Empty;
    
    // ❌ 不應包含業務邏輯方法
    public bool IsValid() => !string.IsNullOrEmpty(ApiUrl);
    
    // ❌ 不應包含領域邏輯
    public GitLabClient CreateClient() => new(ApiUrl);
}
```

#### 2. 預設值策略

**決策**: 使用 `string.Empty` 作為字串屬性的預設值，避免 null 引發的 NullReferenceException

**理由**:
- 專案已啟用 Nullable Reference Types (`<Nullable>enable</Nullable>`)
- `string.Empty` 可避免防禦性 null 檢查
- 必填欄位應透過驗證機制強制，而非依賴 null 判斷

**專案慣例**:
```csharp
// ✅ 字串屬性
public string ApiUrl { get; set; } = string.Empty;

// ✅ 集合屬性
public List<GitLabProjectOptions> Projects { get; set; } = new();

// ✅ 數值屬性（若有選用需求，使用可空型別）
public int? Timeout { get; set; }

// ✅ 布林屬性（需要明確的預設值）
public bool EnableCache { get; set; } = true;
```

#### 3. XML 註解規範

**決策**: 所有公開 Options 類別與屬性必須包含繁體中文 summary 註解

**專案範例** (BitbucketOptions.cs):
```csharp
/// <summary>
/// Bitbucket 設定選項
/// </summary>
public class BitbucketOptions
{
    /// <summary>
    /// Bitbucket API URL
    /// </summary>
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// Bitbucket 電子郵件
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Bitbucket 存取權杖
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;
}
```

### Decision: 設定綁定命名對應規則

**發現**: .NET Configuration Binder 支援不區分大小寫的屬性名稱匹配

**對應規則**:
```json
// appsettings.json - 可使用 PascalCase（建議）
{
  "GitLab": {
    "ApiUrl": "https://gitlab.com/api/v4",
    "AccessToken": ""
  }
}

// 也支援 camelCase（相容性）
{
  "gitLab": {
    "apiUrl": "https://gitlab.com/api/v4",
    "accessToken": ""
  }
}
```

**專案標準**: 
- JSON 設定使用 **PascalCase**（與 C# 屬性名稱一致）
- 提升可讀性與一致性
- 現有 appsettings.json 已遵循此規範

### Decision: 巢狀物件與集合的綁定

**巢狀物件範例** (GitLabOptions + GitLabProjectOptions):

```csharp
// 主 Options 類別
public class GitLabOptions
{
    public List<GitLabProjectOptions> Projects { get; set; } = new();
}

// 巢狀 Options 類別（獨立檔案，遵循 One Class Per File）
public class GitLabProjectOptions
{
    public string ProjectPath { get; set; } = string.Empty;
    public string TargetBranch { get; set; } = string.Empty;
}
```

**對應 JSON 結構**:
```json
{
  "GitLab": {
    "Projects": [
      {
        "ProjectPath": "mygroup/backend-api",
        "TargetBranch": "main"
      }
    ]
  }
}
```

**拆分決策準則**:
- ✅ 巢狀物件有 3 個以上屬性 → 獨立類別
- ✅ 巢狀物件可能在多個 Options 中重複使用 → 獨立類別
- ✅ 巢狀物件代表明確的概念 (如 Project, Mapping) → 獨立類別
- ❌ 只是簡單的 Key-Value 對 → 使用 `Dictionary<string, string>`

**Alternatives Considered**:
- 使用匿名類型 → ❌ 失去類型安全與 IntelliSense
- 使用 Dictionary<string, object> → ❌ 失去類型安全，需要手動轉型
- 將所有屬性平坦化 → ❌ 破壞邏輯分組，降低可讀性

---

## RO-2: 設定驗證策略

### Decision: 採用 IValidateOptions<T> + 啟動時驗證

**選擇的方法**: 分層驗證策略

#### Tier 1: Non-nullable Types（編譯時檢查）
```csharp
// ✅ 對於絕對不能為 null 的設定，使用 non-nullable string
public class RedisOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string InstanceName { get; set; } = string.Empty;
}
```

#### Tier 2: Startup Validation（啟動時檢查）
現有專案範例 (ServiceCollectionExtensions.cs:26-27):
```csharp
var redisConnectionString = configuration["Redis:ConnectionString"] 
    ?? throw new InvalidOperationException("Redis:ConnectionString 組態設定不得為空");
```

**優點**:
- ✅ 簡單直接，無需額外套件
- ✅ 錯誤訊息清晰，立即失敗
- ✅ 適合專案已有的錯誤處理風格

#### Tier 3: IValidateOptions<T>（進階場景）
**使用時機**: 複雜的跨屬性驗證邏輯

```csharp
public class GitLabOptionsValidator : IValidateOptions<GitLabOptions>
{
    public ValidateOptionsResult Validate(string? name, GitLabOptions options)
    {
        if (string.IsNullOrEmpty(options.ApiUrl))
            return ValidateOptionsResult.Fail("GitLab ApiUrl 不得為空");
        
        if (!options.ApiUrl.StartsWith("https://"))
            return ValidateOptionsResult.Fail("GitLab ApiUrl 必須使用 HTTPS");
        
        if (options.Projects.Count == 0)
            return ValidateOptionsResult.Fail("至少需要設定一個 GitLab 專案");
        
        return ValidateOptionsResult.Success;
    }
}

// DI 註冊
services.AddSingleton<IValidateOptions<GitLabOptions>, GitLabOptionsValidator>();
```

### Decision: Data Annotations 不適用

**評估**: System.ComponentModel.DataAnnotations

❌ **不建議使用理由**:
1. 需要額外套件 (`Microsoft.Extensions.Options.DataAnnotations`)
2. 屬性標記 (Attribute) 混合了資料模型與驗證邏輯
3. 違反專案的 SOLID 原則（Options 類別應保持純淨）
4. 驗證錯誤訊息需要本地化支援，增加複雜度

**Alternatives Rejected**:
```csharp
// ❌ 不建議：違反 SRP
public class GitLabOptions
{
    [Required(ErrorMessage = "ApiUrl 為必填欄位")]
    [Url(ErrorMessage = "ApiUrl 必須是有效的 URL")]
    public string ApiUrl { get; set; } = string.Empty;
}
```

### Decision: 驗證時機權衡

| 驗證時機 | 優點 | 缺點 | 建議使用場景 |
|---------|------|------|------------|
| **啟動時驗證** | 立即失敗，阻止錯誤部署 | 無法動態變更設定 | 核心設定（連線字串、API URLs） |
| **延遲驗證** | 允許部分功能降級 | 可能導致執行時錯誤 | 選用功能的設定 |
| **IValidateOptions** | 整合 Options 框架 | 需要額外程式碼 | 複雜驗證邏輯 |

**專案建議**: 
- **預設使用啟動時驗證**（如 RedisOptions 的模式）
- **核心服務的設定必須通過驗證才能啟動**
- **選用功能的設定可以使用空值或預設值降級**

---

## RO-3: 環境特定設定管理

### Decision: 設定載入層次結構

**現有專案配置** (Program.cs:20-29):
```csharp
config
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>(optional: true);
```

**載入順序**（後者覆蓋前者）:
1. `appsettings.json` - 基礎設定與預設值
2. `appsettings.{Environment}.json` - 環境特定覆寫
3. 環境變數 - 容器化部署的動態設定
4. User Secrets - 開發環境的敏感資訊

### Decision: 環境特定覆寫模式

#### 模式 1: 部分屬性覆寫（建議）
**appsettings.json** (基礎設定):
```json
{
  "GitLab": {
    "ApiUrl": "https://gitlab.com/api/v4",
    "AccessToken": "",
    "Projects": [
      {
        "ProjectPath": "mygroup/backend-api",
        "TargetBranch": "main"
      }
    ]
  }
}
```

**appsettings.Development.json** (只覆寫需變更的屬性):
```json
{
  "GitLab": {
    "ApiUrl": "https://gitlab-dev.internal.com/api/v4"
  }
}
```

**結果**: Development 環境的 `AccessToken` 與 `Projects` 繼承自基礎設定

#### 模式 2: 完整區段覆寫（需注意）
若環境檔案定義完整區段，會**完全取代**基礎設定：

**appsettings.Production.json**:
```json
{
  "GitLab": {
    "ApiUrl": "https://gitlab-prod.company.com/api/v4",
    "AccessToken": "${GITLAB_TOKEN}",
    "Projects": [
      {
        "ProjectPath": "company/production-api",
        "TargetBranch": "release"
      }
    ]
  }
}
```

### Decision: User Secrets 使用指南

**啟用條件**: 開發環境 + 已設定 `UserSecretsId`

**專案配置** (ReleaseKit.Console.csproj:31):
```xml
<UserSecretsId>release-kit-secrets-2026</UserSecretsId>
```

**使用時機**:
- ✅ 開發環境的 API 金鑰
- ✅ 開發環境的連線字串
- ✅ 個人開發者專用的 OAuth Token
- ❌ 生產環境設定（User Secrets 不會部署）

**設定方式**:
```bash
# 命令列設定
dotnet user-secrets set "GitLab:AccessToken" "glpat-xxxxxxxxxxxx"

# 或直接編輯 secrets.json
# Windows: %APPDATA%\Microsoft\UserSecrets\release-kit-secrets-2026\secrets.json
# macOS/Linux: ~/.microsoft/usersecrets/release-kit-secrets-2026/secrets.json
```

### Decision: 環境變數覆寫規則

**命名規則**: 使用雙底線 `__` 或冒號 `:` 表示階層

```bash
# 方式 1: 雙底線（跨平台相容）
export GitLab__ApiUrl="https://gitlab.docker.local/api/v4"
export GitLab__AccessToken="glpat-docker-token"

# 方式 2: 冒號（需注意 Shell 轉義）
export "GitLab:ApiUrl"="https://gitlab.docker.local/api/v4"
```

**Docker Compose 範例** (docker-compose.yml):
```yaml
services:
  release-kit:
    environment:
      - GitLab__ApiUrl=https://gitlab.docker.local/api/v4
      - GitLab__AccessToken=${GITLAB_TOKEN}  # 從主機環境變數注入
```

**專案現有範例** (docker-compose.yml 現有的 Redis 設定):
```yaml
- Redis__ConnectionString=redis:6379
```

### Decision: Docker 環境的設定策略

**方法 1: 環境特定檔案** - `appsettings.Docker.json` (專案現有)

**方法 2: 環境變數覆寫** - docker-compose.yml 注入

**建議組合**:
- 不敏感的預設值 → `appsettings.Docker.json`
- 敏感資訊 → 環境變數（從 CI/CD secrets 注入）

---

## RO-4: DI 註冊的組織模式

### Decision: 擴充方法分組策略

**專案現有模式** (ServiceCollectionExtensions.cs):

#### 分組原則: **功能別分組**

```csharp
public static class ServiceCollectionExtensions
{
    // Group 1: Redis 相關服務
    public static IServiceCollection AddRedisServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Redis 連線與服務註冊
    }

    // Group 2: 設定選項
    public static IServiceCollection AddConfigurationOptions(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GitLabOptions>(configuration.GetSection("GitLab"));
        services.Configure<BitbucketOptions>(configuration.GetSection("Bitbucket"));
        services.Configure<UserMappingOptions>(configuration.GetSection("UserMapping"));
    }

    // Group 3: 應用程式服務
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        // 任務、工廠、解析器等註冊
    }
}
```

**優點**:
- ✅ Program.cs 保持簡潔（只有 3 行服務註冊呼叫）
- ✅ 相關服務集中管理，易於維護
- ✅ 符合 Open-Closed Principle（新增服務只需擴充方法）

**Alternatives Considered**:

❌ **方案 A: 層級別分組** (AddDomainServices, AddInfrastructureServices)
- 缺點: 跨層級的功能（如設定）難以分類
- 缺點: 違反專案的實際依賴關係

❌ **方案 B: 單一擴充方法** (AddAllServices)
- 缺點: 違反 SRP，方法過於龐大
- 缺點: 失去分組的可讀性

❌ **方案 C: 在 Program.cs 直接註冊**
- 缺點: Program.cs 膨脹，違反專案憲章 Principle X

### Decision: Configure<T> 的效能考量

**發現**: `services.Configure<T>()` 內部為 Singleton 註冊

**效能影響分析**:
```csharp
// ✅ 高效：設定物件只建立一次
services.Configure<GitLabOptions>(configuration.GetSection("GitLab"));

// ⚠️ 避免：每次解析都重新綁定
services.AddTransient<GitLabOptions>(sp => {
    var config = sp.GetRequiredService<IConfiguration>();
    return config.GetSection("GitLab").Get<GitLabOptions>();
});
```

**專案建議**: 
- 所有設定使用 `Configure<T>()` 而非手動註冊
- 避免在服務生命週期內重複綁定設定

### Decision: 條件式設定註冊

**場景**: 某些設定只在特定環境啟用

```csharp
public static IServiceCollection AddConfigurationOptions(
    this IServiceCollection services, 
    IConfiguration configuration, 
    IHostEnvironment environment)
{
    // 基礎設定（所有環境）
    services.Configure<GitLabOptions>(configuration.GetSection("GitLab"));
    
    // 條件式設定（僅開發環境）
    if (environment.IsDevelopment())
    {
        services.Configure<SeqOptions>(configuration.GetSection("Seq"));
    }
    
    return services;
}
```

**專案現有模式**: Seq 日誌在 Program.cs 條件式設定 (Lines 49-56)
```csharp
if (!string.IsNullOrEmpty(seqServerUrl))
{
    configuration.WriteTo.Seq(seqServerUrl, apiKey: seqApiKey);
}
```

**建議**: 
- 核心設定（如 GitLab, Bitbucket）無條件註冊
- 選用功能（如 Seq, Redis）可條件式註冊或驗證
- 避免過度使用條件式註冊，增加複雜度

---

## Summary & Recommendations

### 核心決策摘要

| 決策領域 | 建議方法 | 理由 |
|---------|---------|------|
| Options 介面 | **IOptions<T>** | Console App 無需熱重載 |
| 預設值 | **string.Empty / new()** | 避免 null 檢查 |
| 驗證策略 | **啟動時驗證 + 選用 IValidateOptions** | 簡單直接，符合專案風格 |
| 環境覆寫 | **部分屬性覆寫** | 減少重複，易於維護 |
| DI 組織 | **功能別分組擴充方法** | 保持 Program.cs 簡潔 |

### 實作優先序

**P0 - 立即執行**:
1. 文件化現有 Options Pattern（data-model.md）
2. 撰寫快速入門指南（quickstart.md）
3. 補充 XML 註解範例

**P1 - 建議補充**:
1. 核心設定的啟動驗證（Redis, GitLab, Bitbucket）
2. JSON Schema 生成（支援 IDE IntelliSense）

**P2 - 未來考慮**:
1. IOptionsMonitor<T> 支援動態重載
2. 設定變更通知機制

### References

- [Microsoft Docs: Options pattern in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options)
- [Microsoft Docs: Configuration in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration)
- [Microsoft Docs: Safe storage of app secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
- 專案現有程式碼: ServiceCollectionExtensions.cs, GitLabOptions.cs, Program.cs
