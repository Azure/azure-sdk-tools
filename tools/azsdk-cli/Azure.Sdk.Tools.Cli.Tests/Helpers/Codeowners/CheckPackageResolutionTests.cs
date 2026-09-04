// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers.Codeowners;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers.Codeowners;

/// <summary>
/// Covers the part of check-package that <see cref="CheckPackageHelperTests"/> does not: rendering
/// the repository and resolving a directory against the result. The ownership rules themselves are
/// tested there against explicit entries.
/// </summary>
internal class CheckPackageResolutionTests
{
    private static async Task<CheckPackageResponse> Check(
        OwnersTestRepo repo,
        string directoryPath,
        IOwnerValidator? ownerValidator = null)
    {
        var validator = ownerValidator ?? OwnerValidatorFake.AcceptAll();

        return await new CheckPackageHelper(new CodeownersModelBuilder(validator), validator)
            .CheckPackage(directoryPath, repo.Root, "Azure/azure-sdk-for-net", CancellationToken.None);
    }

    [Test]
    public async Task ExcludedSectionsDoNotSatisfyOwnership()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.CreateDirectory("sdk/unmigrated/Azure.Unmigrated");

        // /sdk/ in the SDK section matches this path and would own it in the rendered file, but that
        // section is marked exclude-from-check-package, so the package still reads as undeclared.
        var result = await Check(repo, "sdk/unmigrated/Azure.Unmigrated");

        Assert.That(result.Issues.Single().Code, Is.EqualTo(CheckPackageIssue.Codes.NoMatchingPath));
    }

    [Test]
    public async Task OwnershipDeclaredInAnotherServicesFragmentIsNotBorrowed()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.CreateDirectory("sdk/unmigrated/Azure.Unmigrated");

        var result = await Check(repo, "sdk/unmigrated/Azure.Unmigrated");

        Assert.That(result.Owners, Is.Empty);
    }

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
    public async Task ConfiguredMinimumsAreEnforcedInsteadOfAFixedTwo()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteConfig(OwnersTestRepo.ReadAsset("owners.config.yaml")
            .Replace("minimum-path-owners: 2", "minimum-path-owners: 4"));

        // Azure.AI.Inference declares three owners, which passes the default of 2.
        var result = await Check(repo, "sdk/ai/Azure.AI.Inference");

        var issue = result.Issues.Single();
        Assert.That(issue.Code, Is.EqualTo(CheckPackageIssue.Codes.InsufficientOwners));
        Assert.That(issue.RequiredCount, Is.EqualTo(4));
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

    [Test]
    public async Task DroppedOwnersAreReportedSoOwnerCountsStayExplicable()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/ai", """
            version: 1
            paths:
              - path: Azure.AI.Inference/
                owners: [test-user-07, departed-one]
                pr-labels: [AI Model Inference]
            label-owners:
              - labels: [AI Model Inference]
                service-owners: [test-user-07, test-user-09]
            """);

        // Without this, the failure reads "1 unique owner(s)" against a file that lists two.
        var result = await Check(repo, "sdk/ai/Azure.AI.Inference", OwnerValidatorFake.Rejecting("departed-one"));

        Assert.That(result.Issues.Single().Code, Is.EqualTo(CheckPackageIssue.Codes.InsufficientOwners));
        Assert.That(result.DroppedOwners.Select(d => d.Subject), Does.Contain("departed-one"));
    }
}
