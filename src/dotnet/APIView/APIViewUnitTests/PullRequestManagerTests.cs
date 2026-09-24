// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using APIViewWeb.Services;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Octokit;
using Xunit;

namespace APIViewUnitTests;

public class PullRequestManagerTests
{
    private readonly Dictionary<string, string> _configurationValues;
    private readonly List<LanguageService> _languageServices;
    private readonly Mock<IAPIRevisionsManager> _mockApiRevisionsManager;
    private readonly Mock<ICosmosAPIRevisionsRepository> _mockApiRevisionsRepository;
    private readonly Mock<ICodeFileManager> _mockCodeFileManager;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IGitHubClientFactory> _mockGitHubClientFactory;
    private readonly Mock<ILogger<PullRequestManager>> _mockLogger;
    private readonly Mock<ICosmosPullRequestsRepository> _mockPullRequestsRepository;
    private readonly Mock<IReviewManager> _mockReviewManager;
    private readonly Mock<IProjectsManager> _mockProjectManager;
    private readonly TelemetryClient _telemetryClient;

    public PullRequestManagerTests()
    {
        _mockPullRequestsRepository = new Mock<ICosmosPullRequestsRepository>();
        _mockApiRevisionsRepository = new Mock<ICosmosAPIRevisionsRepository>();
        _mockApiRevisionsManager = new Mock<IAPIRevisionsManager>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockCodeFileManager = new Mock<ICodeFileManager>();
        _mockReviewManager = new Mock<IReviewManager>();
        _mockProjectManager = new Mock<IProjectsManager>();
        _mockLogger = new Mock<ILogger<PullRequestManager>>();
        _mockGitHubClientFactory = new Mock<IGitHubClientFactory>();

        TelemetryConfiguration telemetryConfig = TelemetryConfiguration.CreateDefault();
        _telemetryClient = new TelemetryClient(telemetryConfig);
        _languageServices = new List<LanguageService>();
        _configurationValues = new Dictionary<string, string>();

        _mockConfiguration.Setup(c => c[It.IsAny<string>()])
            .Returns<string>(key => _configurationValues.TryGetValue(key, out string value) ? value : null);
    }

    [Theory]
    [InlineData("Azure/azure-sdk-for-net", "Azure", "azure-sdk-for-net")]
    [InlineData("Microsoft/TypeScript", "Microsoft", "TypeScript")]
    [InlineData("dotnet/runtime", "dotnet", "runtime")]
    public async Task GetPullRequestModelAsync_ParsesRepoNameCorrectly(string repoName, string expectedOwner,
        string expectedRepo)
    {
        _configurationValues["GitHubApp:Id"] = "123456";
        _configurationValues["GitHubApp:KeyVaultUrl"] = "https://test.vault.azure.net";
        _configurationValues["GitHubApp:KeyName"] = "test-key";

        _mockPullRequestsRepository
            .Setup(r => r.GetPullRequestAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync((PullRequestModel)null);

        _mockGitHubClientFactory
            .Setup(f => f.CreateGitHubClientAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((GitHubClient)null);

        PullRequestManager manager = new(
            _mockPullRequestsRepository.Object,
            _mockApiRevisionsRepository.Object,
            _mockApiRevisionsManager.Object,
            _mockConfiguration.Object,
            _mockCodeFileManager.Object,
            _mockReviewManager.Object,
            _telemetryClient,
            _mockLogger.Object,
            _languageServices,
            _mockGitHubClientFactory.Object,
            _mockProjectManager.Object);

        await manager.GetPullRequestModelAsync(123, repoName, "Package", "test.json", "C#");
        _mockGitHubClientFactory.Verify(
            f => f.CreateGitHubClientAsync(expectedOwner, expectedRepo),
            Times.Once);
    }

    [Fact]
    public async Task GetPullRequestModelAsync_WhenGitHubClientIsNull_LogsErrorAndReturnsFallback()
    {
        _configurationValues["GitHubApp:Id"] = "123456";
        _configurationValues["GitHubApp:KeyVaultUrl"] = "https://test.vault.azure.net";
        _configurationValues["GitHubApp:KeyName"] = "test-key";

        _mockPullRequestsRepository
            .Setup(r => r.GetPullRequestAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync((PullRequestModel)null);

        _mockGitHubClientFactory
            .Setup(f => f.CreateGitHubClientAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((GitHubClient)null);

        PullRequestManager manager = new(
            _mockPullRequestsRepository.Object,
            _mockApiRevisionsRepository.Object,
            _mockApiRevisionsManager.Object,
            _mockConfiguration.Object,
            _mockCodeFileManager.Object,
            _mockReviewManager.Object,
            _telemetryClient,
            _mockLogger.Object,
            _languageServices,
            _mockGitHubClientFactory.Object,
            _mockProjectManager.Object);


        PullRequestModel result = await manager.GetPullRequestModelAsync(123, "Azure/azure-sdk-for-net", "Azure.Core", "test.json", "C#");
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("GitHub client not available")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }
}
