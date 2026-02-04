// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.TypeSpec;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using GitHub.Copilot.SDK;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.TypeSpec;

/// <summary>
/// Manual integration tests for <see cref="TypeSpecCustomizationService"/> that verify
/// actual LLM calls work correctly through the GitHub Copilot SDK.
/// 
/// These tests are disabled by default because they require GitHub Copilot credentials.
/// To run: dotnet test --filter "FullyQualifiedName~TypeSpecCustomizationServiceLiveTests"
/// </summary>
[TestFixture]
internal class TypeSpecCustomizationServiceLiveTests
{
    private string typeSpecProjectPath = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Get the path to the test TypeSpec project
        typeSpecProjectPath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "TypeSpecTestData",
            "specification",
            "testcontoso",
            "Contoso.Management");
    }

    [SetUp]
    public void SetUp()
    {
        // Clean up any client.tsp from previous runs to ensure a clean slate
        var clientTspPath = Path.Combine(typeSpecProjectPath, "client.tsp");
        if (File.Exists(clientTspPath))
        {
            File.Delete(clientTspPath);
        }
    }

    /// <summary>
    /// Creates a real GitHelper for automatic client reference doc discovery.
    /// </summary>
    private static GitHelper CreateRealGitHelper()
    {
        var rawOutputHelper = Mock.Of<IRawOutputHelper>();
        var gitCommandHelper = new GitCommandHelper(
            new TestLogger<GitCommandHelper>(),
            rawOutputHelper);
        return new GitHelper(
            Mock.Of<IGitHubService>(),
            gitCommandHelper,
            new TestLogger<GitHelper>());
    }

    [Test]
    public async Task ApplyCustomization_WithRealCopilotSdk_CompletesSuccessfully()
    {

        if (!await CopilotTestHelper.IsCopilotAvailableAsync())
        {
            Assert.Ignore("Skipping test as GitHub Copilot CLI is either not installed or not authenticated.");
        }

        var logger = new TestLogger<TypeSpecCustomizationService>();
        var rawOutputHelper = Mock.Of<IRawOutputHelper>();
        var npxHelper = new NpxHelper(
            new TestLogger<NpxHelper>(),
            rawOutputHelper);
        var tokenUsageHelper = new CopilotTokenUsageHelper(rawOutputHelper);
        var gitHelper = CreateRealGitHelper();
        var typeSpecHelper = new TypeSpecHelper(gitHelper);

        // Create real CopilotClient - this will use GitHub credentials
        var copilotClient = new CopilotClient(new CopilotClientOptions
        {
            UseStdio = true,
            AutoStart = true
        });
        var copilotClientWrapper = new CopilotClientWrapper(copilotClient);
        var copilotAgentRunner = new CopilotAgentRunner(
            copilotClientWrapper,
            tokenUsageHelper,
            new TestLogger<CopilotAgentRunner>());

        var service = new TypeSpecCustomizationService(
            logger,
            copilotAgentRunner,
            npxHelper,
            tokenUsageHelper,
            typeSpecHelper,
            gitHelper);

        // A simple customization request
        var customizationRequest = "Add a friendly name 'Contoso Employee' to the Employee model";

        // Let the service discover the reference doc automatically
        var result = await service.ApplyCustomizationAsync(
            typeSpecProjectPath,
            customizationRequest,
            maxIterations: 10);

        // The test verifies the service can run end-to-end with real LLM calls
        // The actual success may depend on the LLM's ability to complete the task
        Assert.That(result, Is.Not.Null);
        
        // Log the result for manual inspection
        TestContext.WriteLine($"Success: {result.Success}");
        if (result.ChangesSummary?.Length > 0)
        {
            TestContext.WriteLine("Changes Summary:");
            foreach (var change in result.ChangesSummary)
            {
                TestContext.WriteLine($"  - {change}");
            }
        }
        if (!result.Success)
        {
            TestContext.WriteLine($"Failure Reason: {result.FailureReason}");
        }
    }

    [Test]
    [Explicit("Manual test - requires GitHub Copilot credentials")]
    public void ApplyCustomization_WithInvalidPath_ThrowsArgumentException()
    {
        var logger = new TestLogger<TypeSpecCustomizationService>();
        var copilotAgentRunner = Mock.Of<ICopilotAgentRunner>();
        var npxHelper = Mock.Of<INpxHelper>();
        var tokenUsageHelper = new CopilotTokenUsageHelper(Mock.Of<IRawOutputHelper>());
        var typeSpecHelper = new TypeSpecHelper(Mock.Of<IGitHelper>());
        var gitHelper = Mock.Of<IGitHelper>();

        var service = new TypeSpecCustomizationService(
            logger,
            copilotAgentRunner,
            npxHelper,
            tokenUsageHelper,
            typeSpecHelper,
            gitHelper);

        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.ApplyCustomizationAsync(
                "/nonexistent/path",
                "Some customization request"));

        Assert.That(ex!.Message, Does.Contain("Invalid TypeSpec project path"));
    }
}
