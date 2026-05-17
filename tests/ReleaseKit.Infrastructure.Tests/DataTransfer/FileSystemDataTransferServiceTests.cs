using Microsoft.Extensions.Logging.Abstractions;
using ReleaseKit.Infrastructure.DataTransfer;

namespace ReleaseKit.Infrastructure.Tests.DataTransfer;

public class FileSystemDataTransferServiceTests : IDisposable
{
    private readonly string _rootDirectory;

    public FileSystemDataTransferServiceTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"release-kit-dt-{Guid.NewGuid():N}");
    }

    [Fact]
    public async Task SetValueAsync_And_GetValueAsync_ShouldWork()
    {
        var service = new FileSystemDataTransferService(_rootDirectory, NullLogger<FileSystemDataTransferService>.Instance);

        await service.SetValueAsync("key-1", "value-1");
        var value = await service.GetValueAsync("key-1");

        Assert.Equal("value-1", value);
    }

    [Fact]
    public async Task SetFieldAsync_And_GetFieldAsync_ShouldWork()
    {
        var service = new FileSystemDataTransferService(_rootDirectory, NullLogger<FileSystemDataTransferService>.Instance);

        await service.SetFieldAsync("hash-1", "field-1", "value-1");
        var value = await service.GetFieldAsync("hash-1", "field-1");

        Assert.Equal("value-1", value);
    }

    [Fact]
    public async Task DeleteFieldAsync_ShouldRemoveField()
    {
        var service = new FileSystemDataTransferService(_rootDirectory, NullLogger<FileSystemDataTransferService>.Instance);

        await service.SetFieldAsync("hash-1", "field-1", "value-1");
        var deleted = await service.DeleteFieldAsync("hash-1", "field-1");
        var exists = await service.FieldExistsAsync("hash-1", "field-1");

        Assert.True(deleted);
        Assert.False(exists);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }
}
