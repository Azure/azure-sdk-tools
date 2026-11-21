// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.ReleasePlan;
using Moq;
using Octokit;
using Build = Microsoft.TeamFoundation.Build.WebApi.Build;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.ReleasePlan;

[TestFixture]
public class ProvisioningSdkToolTests
{
    private const string Project = "specification/maps/resource-manager/Microsoft.Maps/Maps";
    private const string ManagementConfig = """
        # Keep the existing emit list and comments.
        parameters:
          service-dir:
            default: sdk/maps
        emit:
          - "@azure-tools/typespec-autorest"
        options:
          # Management settings must remain unchanged.
          "@azure-typespec/http-client-csharp-mgmt":
            namespace: Azure.ResourceManager.Maps
            emitter-output-dir: "{output-dir}/{service-dir}/{namespace}"
        linter:
          extends: ["@azure-tools/typespec-azure-rulesets/resource-manager"]
        """;
    private const string ProvisioningEntry = """
          "@azure-typespec/http-client-csharp-provisioning":
            namespace: Azure.Provisioning.Maps
            emitter-output-dir: "{output-dir}/{service-dir}/{namespace}"
            api-version: "2025-10-01-preview"

        """;
    private static readonly string Configured = ManagementConfig.Replace("linter:", ProvisioningEntry + "linter:", StringComparison.Ordinal);
    private Mock<IGitHubService> github = null!;
    private Mock<IDevOpsService> devops = null!;
    private ProvisioningSdkTool tool = null!;
    private string directory = null!;

    [SetUp]
    public void SetUp()
    {
        github = new Mock<IGitHubService>(MockBehavior.Strict);
        devops = new Mock<IDevOpsService>(MockBehavior.Strict);
        tool = new ProvisioningSdkTool(github.Object, devops.Object, new TestLogger<ProvisioningSdkTool>());
        directory = Path.Combine(Path.GetTempPath(), $"provisioning-sdk-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(directory, recursive: true);
    }

    [TestCase("\n")]
    [TestCase("\r\n")]
    public async Task Prepare_AddsOnlyProvisioningEntry_AndIsIdempotent(string newline)
    {
        var original = ManagementConfig.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", newline, StringComparison.Ordinal);
        var path = Path.Combine(directory, "tspconfig.yaml");
        await File.WriteAllTextAsync(path, original);

        var result = await tool.PrepareConfigAsync(directory);
        var updated = await File.ReadAllTextAsync(path);
        Assert.That(result.ExitCode, Is.Zero, result.ToString());
        var entry = $"  \"@azure-typespec/http-client-csharp-provisioning\":{newline}" +
            $"    namespace: \"Azure.Provisioning.Maps\"{newline}" +
            $"    emitter-output-dir: '{{output-dir}}/{{service-dir}}/{{namespace}}'{newline}";
        Assert.That(updated, Does.Contain(entry));
        Assert.That(updated.Replace(entry, "", StringComparison.Ordinal), Is.EqualTo(original));

        result = await tool.PrepareConfigAsync(directory);
        Assert.That(result.ExitCode, Is.Zero);
        Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo(updated));
        github.VerifyNoOtherCalls();
        devops.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Prepare_ExistingConfiguration_PreservesApiVersionAndMakesNoChange()
    {
        var path = Path.Combine(directory, "tspconfig.yaml");
        await File.WriteAllTextAsync(path, Configured);
        var result = await tool.PrepareConfigAsync(directory);
        Assert.That(result.ExitCode, Is.Zero, result.ToString());
        Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo(Configured));
    }

    [TestCase("options:\n  '@azure-typespec/http-client-csharp-provisioning': false")]
    [TestCase("options:\n  '@azure-typespec/http-client-csharp-provisioning': {}")]
    [TestCase("options:\n  '@azure-typespec/http-client-csharp-mgmt': {}")]
    [TestCase("options: {}")]
    [TestCase("options: [invalid]")]
    public async Task Prepare_InvalidOrDisabledConfiguration_DoesNotOverwrite(string content)
    {
        var path = Path.Combine(directory, "tspconfig.yaml");
        await File.WriteAllTextAsync(path, content);
        var result = await tool.PrepareConfigAsync(directory);
        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo(content));
    }

    [TestCase(0, false, "refs/heads/main")]
    [TestCase(42, false, "refs/pull/42/merge")]
    [TestCase(42, true, "refs/heads/main")]
    public async Task Generate_SelectsProvisioningPipelineAndSpecRef(int prNumber, bool merged, string expectedRef)
    {
        if (prNumber > 0)
        {
            SetupPr(merged);
        }
        SetupConfig(expectedRef, Configured);
        devops.Setup(s => s.RunPipelineAsync(8473,
                It.Is<Dictionary<string, string>>(p => p.Count == 4 &&
                    p["ConfigType"] == "TypeSpec" && p["ConfigPath"] == $"{Project}/tspconfig.yaml" &&
                    p["CreatePullRequest"] == "true" && p["SdkReleaseType"] == "beta"),
                expectedRef, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Build { Id = 123 });

        var result = await tool.GenerateAsync(Project, "beta", prNumber);

        Assert.That(result.ExitCode, Is.Zero, result.ToString());
        Assert.That(result.ToString(), Does.Contain("buildId=123"));
        github.VerifyAll();
        devops.VerifyAll();
        github.VerifyNoOtherCalls();
        devops.VerifyNoOtherCalls();
    }

    [TestCase(0, "refs/heads/main")]
    [TestCase(42, "refs/pull/42/merge")]
    public async Task Generate_MissingConfig_RequiresSpecPrAndNeverQueues(int prNumber, string expectedRef)
    {
        if (prNumber > 0)
        {
            SetupPr(false);
        }
        SetupConfig(expectedRef, ManagementConfig);

        var result = await tool.GenerateAsync(Project, "beta", prNumber);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.ResponseError, Does.Contain("commit to a spec PR"));
        Assert.That(result.ResponseError, Does.Contain("do not wait for merge"));
        devops.VerifyNoOtherCalls();
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public async Task Generate_ClosedOrConflictingPr_DoesNotFallBackToMain(bool closed, bool conflicting)
    {
        SetupPr(false, closed, conflicting);
        var result = await tool.GenerateAsync(Project, "beta", 42);
        Assert.That(result.ExitCode, Is.EqualTo(1));
        github.VerifyAll();
        github.VerifyNoOtherCalls();
        devops.VerifyNoOtherCalls();
    }

    [TestCase("../maps", "beta")]
    [TestCase("specification/../maps", "beta")]
    [TestCase("specification/maps/tspconfig.yaml", "beta")]
    [TestCase("specification/maps", "preview")]
    public async Task Generate_InvalidInput_NeverQueues(string project, string release)
    {
        var result = await tool.GenerateAsync(project, release);
        Assert.That(result.ExitCode, Is.EqualTo(1));
        github.VerifyNoOtherCalls();
        devops.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Generate_InaccessibleMergeRef_ReportsFailureWithoutQueueing()
    {
        SetupPr(false);
        github.Setup(s => s.GetContentsSingleAsync("Azure", "azure-rest-api-specs", $"{Project}/tspconfig.yaml",
                "refs/pull/42/merge", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("merge ref unavailable"));
        var result = await tool.GenerateAsync(Project, "stable", 42);
        Assert.That(result.ResponseError, Does.Contain("merge ref unavailable"));
        Assert.That(result.ExitCode, Is.EqualTo(1));
        devops.VerifyNoOtherCalls();
    }

    [Test]
    public void Generate_CancellationPropagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        github.Setup(s => s.GetContentsSingleAsync("Azure", "azure-rest-api-specs", $"{Project}/tspconfig.yaml",
                "refs/heads/main", cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));
        Assert.ThrowsAsync<OperationCanceledException>(() => tool.GenerateAsync(Project, "beta", ct: cts.Token));
        devops.VerifyNoOtherCalls();
    }

    private void SetupPr(bool merged, bool closed = false, bool conflicting = false)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            number = 42,
            state = merged || closed ? "closed" : "open",
            merged,
            merged_at = merged ? DateTimeOffset.UtcNow : (DateTimeOffset?)null,
            draft = true,
            mergeable = !conflicting,
            html_url = "https://github.com/Azure/azure-rest-api-specs/pull/42",
            @base = new { @ref = "main" },
        });
        var pr = new Octokit.Internal.SimpleJsonSerializer().Deserialize<PullRequest>(json);
        github.Setup(s => s.GetPullRequestAsync("Azure", "azure-rest-api-specs", 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pr);
    }

    private void SetupConfig(string sourceRef, string content)
    {
        var file = new RepositoryContent("tspconfig.yaml", $"{Project}/tspconfig.yaml", "sha", content.Length,
            ContentType.File, "", "", "", "", "base64", Convert.ToBase64String(Encoding.UTF8.GetBytes(content)), null, null);
        github.Setup(s => s.GetContentsSingleAsync("Azure", "azure-rest-api-specs", $"{Project}/tspconfig.yaml",
                sourceRef, It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);
    }
}
