// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
public class SdkBreakingChangeSuppressionTests
{
    [TestCase("Go", "azure-sdk-for-go")]
    [TestCase("Java", "azure-sdk-for-java")]
    [TestCase("JavaScript", "azure-sdk-for-js")]
    [TestCase("Python", "azure-sdk-for-python")]
    [TestCase(".NET", "azure-sdk-for-net")]
    [TestCase("DotNet", "azure-sdk-for-net")]
    public async Task Classify_ComputesStatusForEachEntryFromExactOriginalMessages(string language, string repoName)
    {
        using var directory = TempDirectory.Create("sdk-suppressions");
        await File.WriteAllTextAsync(Path.Combine(directory.DirectoryPath, "sdk-suppressions.yaml"), $$"""
            suppressions:
              {{repoName}}:
                - package: test-package
                  breaking-changes:
                    - 'Removed Widget'
                    - 'Changed Widget.Name'
                    - 'Changed Widget.Name'
                    - 'Message with "quotes": details'
                - package: other-package
                  breaking-changes:
                    - 'Removed Other'
              azure-sdk-for-rust:
                - package: test-package
                  breaking-changes:
                    - 'Removed Foreign'
            """);
        var result = await ClassifyAsync(directory.DirectoryPath, language, "test-package",
            ["Removed Widget", "Changed Widget.Name"],
            ["Removed Widget", "Removed Other"],
            ["Removed Other"],
            ["Removed Foreign"],
            ["removed Widget"],
            ["Removed Widget "],
            ["Removed"],
            ["Message with \"quotes\": details"],
            [],
            null);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.BreakingChanges.Select(change => change.SuppressionStatus), Is.EqualTo(new[]
        {
            SdkBreakingChangeSuppressionStatus.Suppressed,
            SdkBreakingChangeSuppressionStatus.PartiallySuppressed,
            SdkBreakingChangeSuppressionStatus.None,
            SdkBreakingChangeSuppressionStatus.None,
            SdkBreakingChangeSuppressionStatus.None,
            SdkBreakingChangeSuppressionStatus.None,
            SdkBreakingChangeSuppressionStatus.None,
            SdkBreakingChangeSuppressionStatus.Suppressed,
            SdkBreakingChangeSuppressionStatus.None,
            SdkBreakingChangeSuppressionStatus.None,
        }));
        Assert.That(result.HasBreakingChange, Is.True);
        Assert.That(result.BreakingChanges[1].OriginBreaks, Is.EqualTo(new[] { "Removed Widget", "Removed Other" }));
        var json = JsonSerializer.SerializeToElement(result);
        Assert.That(json.GetProperty("breakingChanges")[0].GetProperty("suppressionStatus").GetString(), Is.EqualTo("suppressed"));
        Assert.That(json.GetProperty("breakingChanges")[1].GetProperty("suppressionStatus").GetString(), Is.EqualTo("partiallySuppressed"));
        Assert.That(json.GetProperty("breakingChanges")[2].GetProperty("suppressionStatus").GetString(), Is.EqualTo("none"));
    }

    [TestCase(null, "Go", "test-package")]
    [TestCase("suppressions: {}", "Go", "test-package")]
    [TestCase("suppressions: { azure-sdk-for-go: [] }", "Go", "test-package")]
    [TestCase("suppressions: { azure-sdk-for-go: [] }", "Unknown", "test-package")]
    [TestCase("suppressions: { azure-sdk-for-go: [] }", "Go", null)]
    public async Task Classify_NoApplicableSuppressionsResetsAgentStatus(string? yaml, string language, string? packageName)
    {
        using var directory = TempDirectory.Create("sdk-suppressions");
        if (yaml != null)
        {
            await File.WriteAllTextAsync(Path.Combine(directory.DirectoryPath, "sdk-suppressions.yaml"), yaml);
        }

        var result = await ClassifyAsync(directory.DirectoryPath, language, packageName, ["Removed Widget"]);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.BreakingChanges.Single().SuppressionStatus, Is.EqualTo(SdkBreakingChangeSuppressionStatus.None));
    }

    [Test]
    public async Task Classify_UnreadableSuppressionsReportsFailure()
    {
        using var directory = TempDirectory.Create("sdk-suppressions");
        using var lockedFile = new FileStream(Path.Combine(directory.DirectoryPath, "sdk-suppressions.yaml"),
            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        var logger = new TestLogger<SdkBreakingChangeClassificationService>();

        var result = await ClassifyAsync(directory.DirectoryPath, "Go", "test-package", logger, ["Removed Widget"]);

        Assert.That(result, Is.Null);
        Assert.That(logger.Logs.Any(log => log.ToString()!.Contains("load SDK suppressions")), Is.True);
    }

    [TestCase("other-package")]
    [TestCase("TEST-PACKAGE")]
    [TestCase(null)]
    public async Task Classify_RequiresExactPackageName(string? packageName)
    {
        using var directory = TempDirectory.Create("sdk-suppressions");
        await File.WriteAllTextAsync(Path.Combine(directory.DirectoryPath, "sdk-suppressions.yaml"), """
            suppressions:
              azure-sdk-for-go:
                - package: test-package
                  breaking-changes:
                    - Removed Widget
            """);
        var logger = new TestLogger<SdkBreakingChangeClassificationService>();

        var result = await ClassifyAsync(directory.DirectoryPath, "Go", packageName, logger, ["Removed Widget"]);

        Assert.That(result!.BreakingChanges.Single().SuppressionStatus, Is.EqualTo(SdkBreakingChangeSuppressionStatus.None));
        if (packageName == null)
        {
            Assert.That(logger.Logs.Any(log => log.ToString()!.Contains("without an SDK package name")), Is.True);
        }
    }

    [Test]
    public async Task Classify_NoProjectPathResetsAgentStatus()
    {
        var result = await ClassifyAsync(null, "Go", "test-package", ["Removed Widget"]);

        Assert.That(result!.BreakingChanges.Single().SuppressionStatus, Is.EqualTo(SdkBreakingChangeSuppressionStatus.None));
    }

    [TestCase("")]
    [TestCase("suppressions: [")]
    [TestCase("other: {}")]
    [TestCase("suppressions: { azure-sdk-for-go: [{ package: test-package }] }")]
    [TestCase("suppressions: { azure-sdk-for-go: [{ breaking-changes: [] }] }")]
    [TestCase("suppressions: { azure-sdk-for-go: null }")]
    [TestCase("suppressions: []")]
    [TestCase("suppressions: { azure-sdk-for-go: {} }")]
    [TestCase("suppressions: { azure-sdk-for-go: [test-package] }")]
    [TestCase("suppressions: { azure-sdk-for-go: [{ package: [test-package], breaking-changes: [] }] }")]
    [TestCase("suppressions: { azure-sdk-for-go: [{ package: test-package, breaking-changes: message }] }")]
    [TestCase("suppressions: { azure-sdk-for-go: [{ package: test-package, breaking-changes: [{}] }] }")]
    [TestCase("suppressions: {}\n---\nsuppressions: {}")]
    [TestCase("suppressions: { azure-sdk-for-go: [{ package: test-package, breaking-changes: [null] }] }")]
    [TestCase("suppressions: { azure-sdk-for-go: [{ package: test-package, breaking-changes: [], extra: true }] }")]
    [TestCase("suppressions: {}\nsuppressions: {}")]
    public async Task Classify_InvalidSuppressionsReportsFailure(string yaml)
    {
        using var directory = TempDirectory.Create("sdk-suppressions");
        await File.WriteAllTextAsync(Path.Combine(directory.DirectoryPath, "sdk-suppressions.yaml"), yaml);
        var logger = new TestLogger<SdkBreakingChangeClassificationService>();

        var result = await ClassifyAsync(directory.DirectoryPath, "Go", "test-package", logger, ["Removed Widget"]);

        Assert.That(result, Is.Null);
        Assert.That(logger.Logs.Any(log => log.ToString()!.Contains("load SDK suppressions")), Is.True);
    }

    private static Task<SdkBreakingChangeDetectionResult?> ClassifyAsync(
        string? path, string language, string? packageName, params string[]?[] origins)
    {
        return ClassifyAsync(path, language, packageName, new TestLogger<SdkBreakingChangeClassificationService>(), origins);
    }

    private static Task<SdkBreakingChangeDetectionResult?> ClassifyAsync(
        string? path, string language, string? packageName,
        TestLogger<SdkBreakingChangeClassificationService> logger, params string[]?[] origins)
    {
        var runner = new Mock<ICopilotAgentRunner>();
        runner.Setup(r => r.RunAsync(It.IsAny<CopilotAgent<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(new
            {
                hasBreakingChange = true,
                breakingChanges = origins.Select(origin => new
                {
                    breakingChange = "Grouped change",
                    category = "unknown",
                    originBreaks = origin,
                    suppressionStatus = "suppressed",
                }),
            }));
        return new SdkBreakingChangeClassificationService(runner.Object, logger)
            .ClassifySdkBreakingChangesAsync("Changes", "Patterns", language, path, packageName, CancellationToken.None);
    }
}
