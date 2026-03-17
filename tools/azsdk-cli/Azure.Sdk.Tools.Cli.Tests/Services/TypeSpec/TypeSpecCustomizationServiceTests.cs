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
/// Tests for <see cref="TypeSpecCustomizationService"/>.
/// </summary>
[TestFixture]
internal class TypeSpecCustomizationServiceTests
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

    /// <summary>
    /// Live integration test that makes actual LLM calls through the GitHub Copilot SDK.
    /// Requires GitHub Copilot CLI to be installed and authenticated.
    /// Will be skipped if Copilot is not available.
    /// </summary>
    [Test]
    [Explicit]  // Mark as explicit/manual because this test takes 26 seconds
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
        var tokenUsageHelper = new TokenUsageHelper(rawOutputHelper);
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
    public void ApplyCustomization_WithInvalidPath_ThrowsArgumentException()
    {
        var logger = new TestLogger<TypeSpecCustomizationService>();
        var copilotAgentRunner = Mock.Of<ICopilotAgentRunner>();
        var npxHelper = Mock.Of<INpxHelper>();
        var tokenUsageHelper = new TokenUsageHelper(Mock.Of<IRawOutputHelper>());
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

    [Test]
    public void ApplyCustomization_WithInvalidReferenceDocPath_ThrowsFileNotFoundException()
    {
        var logger = new TestLogger<TypeSpecCustomizationService>();
        var copilotAgentRunner = Mock.Of<ICopilotAgentRunner>();
        var npxHelper = Mock.Of<INpxHelper>();
        var tokenUsageHelper = new TokenUsageHelper(Mock.Of<IRawOutputHelper>());
        var gitHelper = CreateRealGitHelper();
        var typeSpecHelper = new TypeSpecHelper(gitHelper);

        var service = new TypeSpecCustomizationService(
            logger,
            copilotAgentRunner,
            npxHelper,
            tokenUsageHelper,
            typeSpecHelper,
            gitHelper);

        var ex = Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await service.ApplyCustomizationAsync(
                typeSpecProjectPath,
                "Some customization request",
                referenceDocPath: "/nonexistent/customizing-client-tsp.md"));

        Assert.That(ex!.Message, Does.Contain("Reference document not found"));
    }

    [Test]
    public async Task ReferenceDocDiscovery_FindsDocumentInEngCommonKnowledge()
    {
        var gitHelper = CreateRealGitHelper();

        // Discover repo root from the test project path
        var repoRoot = await gitHelper.DiscoverRepoRootAsync(typeSpecProjectPath, CancellationToken.None);
        Assert.That(repoRoot, Is.Not.Null.And.Not.Empty, "Should find repository root");

        // Check that the reference doc exists at the expected location
        var expectedPath = Path.Combine(repoRoot!, "eng", "common", "knowledge", "customizing-client-tsp.md");
        Assert.That(File.Exists(expectedPath), Is.True,
            $"Reference document should exist at {expectedPath}");
    }
}
