using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Infrastructure.Analysis;

namespace ReleaseKit.Infrastructure.Tests.Analysis;

/// <summary>
/// ProjectStructureScanner 單元測試
/// </summary>
public class ProjectStructureScannerTests : IDisposable
{
    private readonly ProjectStructureScanner _scanner;
    private readonly string _tempDir;

    public ProjectStructureScannerTests()
    {
        _scanner = new ProjectStructureScanner(Mock.Of<ILogger<ProjectStructureScanner>>());
        _tempDir = Path.Combine(Path.GetTempPath(), $"scanner-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void ScanNuGetPackages_應從csproj取得套件引用()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Test.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
                <PackageReference Include="Serilog" Version="3.0.0" />
              </ItemGroup>
            </Project>
            """);

        var result = _scanner.ScanNuGetPackages(_tempDir);

        Assert.Equal(2, result.Count);
        Assert.Contains("Newtonsoft.Json", result);
        Assert.Contains("Serilog", result);
    }

    [Fact]
    public void ScanNuGetPackages_多個csproj應合併不重複套件()
    {
        var subDir = Path.Combine(_tempDir, "SubProject");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(_tempDir, "A.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Serilog" Version="3.0.0" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(subDir, "B.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Serilog" Version="3.0.0" />
                <PackageReference Include="FluentAssertions" Version="7.0.0" />
              </ItemGroup>
            </Project>
            """);

        var result = _scanner.ScanNuGetPackages(_tempDir);

        Assert.Equal(2, result.Count);
        Assert.Contains("Serilog", result);
        Assert.Contains("FluentAssertions", result);
    }

    [Fact]
    public void ScanNuGetPackages_無csproj時應回傳空清單()
    {
        var result = _scanner.ScanNuGetPackages(_tempDir);

        Assert.Empty(result);
    }

    [Fact]
    public void ScanApiEndpoints_應解析Controller的HttpMethod與Route()
    {
        var controllerDir = Path.Combine(_tempDir, "Controllers");
        Directory.CreateDirectory(controllerDir);
        File.WriteAllText(Path.Combine(controllerDir, "UserController.cs"),
            """
            [Route("api/v1/users")]
            public class UserController : ControllerBase
            {
                [HttpGet]
                public IActionResult GetUsers() { }

                [HttpGet("{id}")]
                public IActionResult GetUser() { }

                [HttpPost]
                public IActionResult CreateUser() { }
            }
            """);

        var result = _scanner.ScanApiEndpoints(_tempDir);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, e => e.HttpMethod == "GET" && e.ActionName == "GetUsers");
        Assert.Contains(result, e => e.HttpMethod == "GET" && e.ActionName == "GetUser");
        Assert.Contains(result, e => e.HttpMethod == "POST" && e.ActionName == "CreateUser");
    }

    [Fact]
    public void ScanApiEndpoints_應正確組合baseRoute與actionRoute()
    {
        var controllerDir = Path.Combine(_tempDir, "Controllers");
        Directory.CreateDirectory(controllerDir);
        File.WriteAllText(Path.Combine(controllerDir, "OrderController.cs"),
            """
            [Route("api/v1/orders")]
            public class OrderController : ControllerBase
            {
                [HttpGet("{orderId}/items")]
                public IActionResult GetOrderItems() { }
            }
            """);

        var result = _scanner.ScanApiEndpoints(_tempDir);

        Assert.Single(result);
        Assert.Equal("api/v1/orders/{orderId}/items", result[0].Route);
        Assert.Equal("OrderController", result[0].ControllerName);
    }

    [Fact]
    public void ScanApiEndpoints_無Controller時應回傳空清單()
    {
        var result = _scanner.ScanApiEndpoints(_tempDir);

        Assert.Empty(result);
    }

    [Fact]
    public void ScanDbContextFiles_應找到DbContext檔案()
    {
        var dataDir = Path.Combine(_tempDir, "Data");
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(Path.Combine(dataDir, "AppDbContext.cs"), "public class AppDbContext {}");

        var result = _scanner.ScanDbContextFiles(_tempDir);

        Assert.Single(result);
        Assert.Contains("AppDbContext.cs", result[0]);
    }

    [Fact]
    public void ScanDbContextFiles_應回傳相對路徑()
    {
        var dataDir = Path.Combine(_tempDir, "Data");
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(Path.Combine(dataDir, "AppDbContext.cs"), "public class AppDbContext {}");

        var result = _scanner.ScanDbContextFiles(_tempDir);

        Assert.DoesNotContain(result, r => Path.IsPathRooted(r));
    }

    [Fact]
    public void ScanDbContextFiles_無DbContext時應回傳空清單()
    {
        var result = _scanner.ScanDbContextFiles(_tempDir);

        Assert.Empty(result);
    }

    [Fact]
    public void ScanMigrationFiles_應找到Migrations目錄下的檔案()
    {
        var migDir = Path.Combine(_tempDir, "Migrations");
        Directory.CreateDirectory(migDir);
        File.WriteAllText(Path.Combine(migDir, "20260101_Init.cs"), "class Init {}");
        File.WriteAllText(Path.Combine(migDir, "20260202_AddUsers.cs"), "class AddUsers {}");

        var result = _scanner.ScanMigrationFiles(_tempDir);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ScanMigrationFiles_非Migrations目錄的cs檔不應被包含()
    {
        File.WriteAllText(Path.Combine(_tempDir, "SomeService.cs"), "class SomeService {}");

        var result = _scanner.ScanMigrationFiles(_tempDir);

        Assert.Empty(result);
    }

    [Fact]
    public void ScanMessageContracts_應找到Event和Command檔案()
    {
        var eventsDir = Path.Combine(_tempDir, "Events");
        Directory.CreateDirectory(eventsDir);
        File.WriteAllText(Path.Combine(eventsDir, "OrderCreatedEvent.cs"), "class OrderCreatedEvent {}");
        File.WriteAllText(Path.Combine(eventsDir, "CreateOrderCommand.cs"), "class CreateOrderCommand {}");

        var result = _scanner.ScanMessageContracts(_tempDir);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ScanMessageContracts_應找到Message檔案()
    {
        File.WriteAllText(Path.Combine(_tempDir, "PaymentMessage.cs"), "class PaymentMessage {}");

        var result = _scanner.ScanMessageContracts(_tempDir);

        Assert.Single(result);
        Assert.Contains("PaymentMessage.cs", result[0]);
    }

    [Fact]
    public void ScanMessageContracts_無契約檔案時應回傳空清單()
    {
        var result = _scanner.ScanMessageContracts(_tempDir);

        Assert.Empty(result);
    }

    [Fact]
    public void ScanConfigKeys_應解析appsettings的JSON路徑()
    {
        File.WriteAllText(Path.Combine(_tempDir, "appsettings.json"),
            """
            {
              "ConnectionStrings": {
                "DefaultConnection": "Server=localhost"
              },
              "Logging": {
                "LogLevel": {
                  "Default": "Information"
                }
              }
            }
            """);

        var result = _scanner.ScanConfigKeys(_tempDir);

        Assert.Contains("ConnectionStrings:DefaultConnection", result);
        Assert.Contains("Logging:LogLevel:Default", result);
    }

    [Fact]
    public void ScanConfigKeys_應合併多個appsettings檔案的key()
    {
        File.WriteAllText(Path.Combine(_tempDir, "appsettings.json"),
            """{"Feature": {"Enabled": true}}""");
        File.WriteAllText(Path.Combine(_tempDir, "appsettings.Production.json"),
            """{"Feature": {"Url": "https://example.com"}}""");

        var result = _scanner.ScanConfigKeys(_tempDir);

        Assert.Contains("Feature:Enabled", result);
        Assert.Contains("Feature:Url", result);
    }

    [Fact]
    public void ScanConfigKeys_無appsettings時應回傳空清單()
    {
        var result = _scanner.ScanConfigKeys(_tempDir);

        Assert.Empty(result);
    }

    [Fact]
    public void Scan_應回傳完整ProjectStructure()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Test.csproj"),
            """<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><PackageReference Include="TestPkg" Version="1.0" /></ItemGroup></Project>""");

        var result = _scanner.Scan("test/project", _tempDir);

        Assert.Equal("test/project", result.ProjectPath);
        Assert.Single(result.NuGetPackages);
        Assert.Empty(result.InferredDependencies);
    }
}
