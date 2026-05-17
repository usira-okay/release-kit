using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Console.Factories;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Infrastructure.FileStorage;
using ReleaseKit.Infrastructure.Redis;
using StackExchange.Redis;

namespace ReleaseKit.Console.Tests.Factories;

/// <summary>
/// DataTransferServiceFactory 單元測試
/// </summary>
public class DataTransferServiceFactoryTests
{
    [Fact]
    public void Create_ShouldReturnFileDataTransferService_WhenProviderIsFileSystem()
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["DataTransfer:Provider"] = "FileSystem",
                ["FileStorage:BasePath"] = "/tmp/release-kit-test-data"
            });
        var factory = new DataTransferServiceFactory(
            configuration,
            new FakeNow(),
            LoggerFactory.Create(_ => { }),
            new FakeRedisConnectionFactory());

        var service = factory.Create();

        Assert.IsType<FileDataTransferService>(service);
    }

    [Fact]
    public void Create_ShouldReturnRedisService_WhenProviderIsRedis()
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["DataTransfer:Provider"] = "Redis",
                ["Redis:ConnectionString"] = "localhost:6379",
                ["Redis:InstanceName"] = "ReleaseKit:"
            });
        var factory = new DataTransferServiceFactory(
            configuration,
            new FakeNow(),
            LoggerFactory.Create(_ => { }),
            new FakeRedisConnectionFactory());

        var service = factory.Create();

        Assert.IsType<RedisService>(service);
    }

    [Fact]
    public void Create_ShouldThrowInvalidOperationException_WhenProviderIsMissing()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>());
        var factory = new DataTransferServiceFactory(
            configuration,
            new FakeNow(),
            LoggerFactory.Create(_ => { }),
            new FakeRedisConnectionFactory());

        var action = () => factory.Create();

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Equal("DataTransfer:Provider 組態設定不得為空", exception.Message);
    }

    [Fact]
    public void Create_ShouldThrowInvalidOperationException_WhenProviderIsUnsupported()
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["DataTransfer:Provider"] = "Database"
            });
        var factory = new DataTransferServiceFactory(
            configuration,
            new FakeNow(),
            LoggerFactory.Create(_ => { }),
            new FakeRedisConnectionFactory());

        var action = () => factory.Create();

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Contains("DataTransfer:Provider 組態設定僅支援 Redis 或 FileSystem", exception.Message);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private sealed class FakeNow : INow
    {
        public DateTimeOffset UtcNow => new(2025, 5, 17, 0, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeRedisConnectionFactory : IRedisConnectionFactory
    {
        public IConnectionMultiplexer Create(string connectionString, ILogger logger)
        {
            return new Mock<IConnectionMultiplexer>().Object;
        }
    }
}
