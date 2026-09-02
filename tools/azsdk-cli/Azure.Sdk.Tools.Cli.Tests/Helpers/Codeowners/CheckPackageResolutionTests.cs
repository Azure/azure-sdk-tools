// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers.Codeowners;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers.Codeowners;

/// <summary>
/// Covers the part of check-package that <see cref="CheckPackageHelperTests"/> does not: finding the
/// fragment that governs a directory and turning it into entries. The ownership rules themselves are
/// tested there against explicit entries.
/// </summary>
internal class CheckPackageResolutionTests
{
    private static async Task<CheckPackageResponse> Check(OwnersTestRepo repo, string directoryPath) =>
        await new CheckPackageHelper().CheckPackage(directoryPath, repo.Root, "Azure/azure-sdk-for-net", CancellationToken.None);

    [Test]
    public async Task PackageWithItsOwnPathEntryPasses()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        var result = await Check(repo, "sdk/ai/Azure.AI.Inference");

        Assert.That(result.Issues, Is.Empty);
        Assert.That(result.MatchedPathExpression, Is.EqualTo("/sdk/ai/Azure.AI.Inference/"));
        Assert.That(result.Owners, Is.EquivalentTo(new[] { "test-user-07", "test-user-09", "test-user-23" }));
    }

    [Test]
    public async Task PackageWithoutItsOwnEntryFallsBackToTheServiceDirectory()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.CreateDirectory("sdk/ai/Azure.AI.Unlisted");

        var result = await Check(repo, "sdk/ai/Azure.AI.Unlisted");

        Assert.That(result.Issues, Is.Empty);
        Assert.That(result.MatchedPathExpression, Is.EqualTo("/sdk/ai/"));
        Assert.That(result.ResolvedTargetType, Is.EqualTo("path"));
    }

    [Test]
    public async Task ServiceOwnersComeFromTheFragmentsLabelOwnersBlock()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        var result = await Check(repo, "sdk/ai/Azure.AI.Inference");

        Assert.That(result.ServiceLabels, Is.EquivalentTo(new[] { "AI Model Inference" }));
        Assert.That(result.ServiceOwners, Is.EquivalentTo(new[] { "test-user-07", "test-user-09", "test-user-23" }));
    }

    [Test]
    public async Task DirectoryWithNoFragmentAboveItReportsUnowned()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.CreateDirectory("sdk/unmigrated/Azure.Unmigrated");

        var result = await Check(repo, "sdk/unmigrated/Azure.Unmigrated");

        var issue = result.Issues.Single();
        Assert.That(issue.Code, Is.EqualTo(CheckPackageIssue.Codes.NoMatchingPath));
        Assert.That(issue.NextStep, Does.Contain("sdk/unmigrated/owners.yaml"));
    }

    [Test]
    public async Task IssuesNameTheFragmentTheAuthorHasToEdit()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/openai",
            """
            version: 1
            paths:
              - path: .
                owners: [test-user-02]
                pr-labels: [OpenAI]
            label-owners:
              - labels: [OpenAI]
                service-owners: [test-user-02, test-user-24]
            """);

        var result = await Check(repo, "sdk/openai/Azure.AI.OpenAI");

        var issue = result.Issues.Single();
        Assert.That(issue.Code, Is.EqualTo(CheckPackageIssue.Codes.InsufficientOwners));
        Assert.That(issue.NextStep, Does.Contain("sdk/openai/owners.yaml"));
    }
}
