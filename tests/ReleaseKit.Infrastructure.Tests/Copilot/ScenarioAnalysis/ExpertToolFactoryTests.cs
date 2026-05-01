using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis;

namespace ReleaseKit.Infrastructure.Tests.Copilot.ScenarioAnalysis;

/// <summary>
/// ExpertToolFactory 單元測試
/// </summary>
public class ExpertToolFactoryTests
{
    private readonly Mock<IGitOperationService> _gitServiceMock;
    private readonly ExpertToolFactory _sut;

    public ExpertToolFactoryTests()
    {
        _gitServiceMock = new Mock<IGitOperationService>();
        var logger = new Mock<ILogger<ExpertToolFactory>>();
        _sut = new ExpertToolFactory(_gitServiceMock.Object, logger.Object);
    }

    [Fact]
    public void CreateTools_應回傳5個工具()
    {
        var tools = _sut.CreateTools("/repo/path", CancellationToken.None);
        tools.Should().HaveCount(5);
    }

    [Fact]
    public void CreateTools_應包含所有必要工具名稱()
    {
        var tools = _sut.CreateTools("/repo/path", CancellationToken.None);
        var names = tools.Select(t => t.Name).ToList();

        names.Should().Contain("get_commit_overview");
        names.Should().Contain("get_full_diff");
        names.Should().Contain("get_file_content");
        names.Should().Contain("search_pattern");
        names.Should().Contain("list_directory");
    }
}
