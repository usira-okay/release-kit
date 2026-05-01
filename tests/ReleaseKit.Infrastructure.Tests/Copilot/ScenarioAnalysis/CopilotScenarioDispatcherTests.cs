using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis;
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis.Models;

namespace ReleaseKit.Infrastructure.Tests.Copilot.ScenarioAnalysis;

/// <summary>
/// CopilotScenarioDispatcher 單元測試（驗證協調邏輯，mock Agent Runners）
/// </summary>
public class CopilotScenarioDispatcherTests
{
    [Fact]
    public void BuildClientOptions_有Token_應設定Token()
    {
        var options = Options.Create(new CopilotOptions
        {
            GitHubToken = "test-token",
            Model = "gpt-4.1",
            TimeoutSeconds = 300
        });

        var dispatcher = new CopilotScenarioDispatcher(
            null!,
            null!,
            null!,
            Mock.Of<ILogger<CopilotScenarioDispatcher>>(),
            options);

        var clientOptions = dispatcher.BuildClientOptions();
        clientOptions.GitHubToken.Should().Be("test-token");
    }

    [Fact]
    public void BuildClientOptions_無Token_不應拋出()
    {
        var options = Options.Create(new CopilotOptions
        {
            GitHubToken = "",
            Model = "gpt-4.1"
        });

        var dispatcher = new CopilotScenarioDispatcher(
            null!,
            null!,
            null!,
            Mock.Of<ILogger<CopilotScenarioDispatcher>>(),
            options);

        var clientOptions = dispatcher.BuildClientOptions();
        clientOptions.Should().NotBeNull();
    }
}
