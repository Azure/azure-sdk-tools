// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers.Codeowners;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class CheckPackageHelperTests
{
    private CheckPackageHelper helper = null!;
    private List<CodeownersEntry> entries = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var fixturePath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "TestAssets",
            "check-package-test-codeowners.txt");

        Assert.That(File.Exists(fixturePath), Is.True,
            $"Test fixture not found at {fixturePath}");

        entries = CodeownersParser.ParseCodeownersFile(fixturePath);
    }

    [SetUp]
    public void SetUp()
    {
        helper = new CheckPackageHelper();
    }

    [Test]
    public void CheckPackage_TwoOwners_ValidServiceOwners_Passes()
    {
        var result = helper.CheckPackage(
            "sdk/two-owners/Azure.TwoOwners",
            "Azure/azure-sdk-for-net",
            entries);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.Issues, Is.Empty);
        Assert.That(result.DirectoryPath, Is.EqualTo("sdk/two-owners/Azure.TwoOwners"));
        Assert.That(result.ResolvedTargetType, Is.EqualTo("package"));
        Assert.That(result.ResolvedTarget, Is.EqualTo("/sdk/two-owners/Azure.TwoOwners"));
        Assert.That(result.Owners.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(result.PRLabels, Does.Contain("TwoOwners"));
        Assert.That(result.ServiceOwners.Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void CheckPackage_ThreeOwners_ValidServiceOwners_Passes()
    {
        var result = helper.CheckPackage(
            "sdk/three-owners/Azure.ThreeOwners",
            "Azure/azure-sdk-for-net",
            entries);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.Owners.Count, Is.EqualTo(3));
        Assert.That(result.ServiceOwners.Count, Is.EqualTo(3));
    }

    [Test]
    public void CheckPackage_OneOwner_ReturnsFailure()
    {
        var result = helper.CheckPackage(
            "sdk/one-owner/Azure.OneOwner",
            "Azure/azure-sdk-for-net",
            entries);

        AssertFailure(result, "insufficient_owners", "1 unique owner");
        Assert.That(result.Issues[0].NextStep, Does.Contain($"/owners add owners {CheckPackageHelper.CurrentGitHubUserPlaceholder}"));
    }

    [Test]
    public void CheckPackage_NoPrLabels_ReturnsFailure()
    {
        var result = helper.CheckPackage(
            "sdk/no-labels/Azure.NoLabels",
            "Azure/azure-sdk-for-net",
            entries);

        AssertFailure(result, "missing_pr_label", "has no PR label");
        Assert.That(result.Issues[0].NextStep, Does.Contain("/owners add label \"<pr-label>\" to package Azure.NoLabels"));
    }

    [Test]
    public void CheckPackage_ZeroServiceOwners_ReturnsFailure()
    {
        var result = helper.CheckPackage(
            "sdk/zero-svc-owners/Azure.ZeroSvcOwners",
            "Azure/azure-sdk-for-net",
            entries);

        AssertFailure(result, "insufficient_service_owners", "PR label \"ZeroOwners\" has 0 unique service owner(s)");
        Assert.That(result.Issues[0].NextStep, Does.StartWith($"/owners add service owners {CheckPackageHelper.CurrentGitHubUserPlaceholder}"));
        Assert.That(result.Issues[0].NextStep, Does.Contain("to label \"ZeroOwners\""));
    }

    [Test]
    public void CheckPackage_NoMatchingServiceLabel_ReturnsFailure()
    {
        var result = helper.CheckPackage(
            "sdk/no-svc-match/Azure.NoSvcMatch",
            "Azure/azure-sdk-for-net",
            entries);

        AssertFailure(result, "insufficient_service_owners", "PR label \"NoMatchingSvcLabel\" has 0 unique service owner(s)");
        Assert.That(result.Issues[0].NextStep, Does.Contain("to label \"NoMatchingSvcLabel\""));
        Assert.That(result.ServiceLabels, Does.Contain("NoMatchingSvcLabel"));
    }

    [Test]
    public void CheckPackage_NoMatchingPath_ReturnsFailure()
    {
        var result = helper.CheckPackage(
            "sdk/does-not-exist/Azure.NonExistent",
            "Azure/azure-sdk-for-net",
            entries);

        AssertFailure(result, "no_matching_path", "No CODEOWNERS entry matches path");
        Assert.That(result.Issues[0].NextStep, Does.StartWith("/owners inspect path "));
    }

    [Test]
    public void CheckPackage_MultipleIssues_AreCollected()
    {
        var customEntries = new List<CodeownersEntry>
        {
            new()
            {
                PathExpression = "/sdk/test/Azure.Test/",
                SourceOwners = ["ownerAlice"],
                PRLabels = ["TestLabel"],
            }
        };

        var result = helper.CheckPackage(
            "sdk/test/Azure.Test",
            "Azure/azure-sdk-for-net",
            customEntries);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Failed));
        Assert.That(result.Issues, Has.Count.EqualTo(2));
        Assert.That(result.Issues.Select(issue => issue.Code), Is.EquivalentTo(new[]
        {
            "insufficient_owners",
            "insufficient_service_owners",
        }));
    }

    [Test]
    public void CheckPackage_ServiceLevelPathEntry_OneOwner_ReturnsPathScopedOwnerPrompt()
    {
        var customEntries = CreateEntries(
            pathExpression: "/sdk/service/",
            sourceOwners: ["ownerAlice"],
            prLabels: ["Label1"],
            serviceOwners: ["serviceOwnerAlice", "serviceOwnerBob"]);

        var result = helper.CheckPackage(
            "sdk/service/Package.Name",
            "Azure/azure-sdk-for-net",
            customEntries);

        AssertFailure(result, "insufficient_owners", "resolved service-level path entry '/sdk/service'");
        Assert.That(result.ResolvedTargetType, Is.EqualTo("path"));
        Assert.That(result.ResolvedTarget, Is.EqualTo("/sdk/service"));
        Assert.That(result.Issues[0].NextStep, Does.Contain($"/owners add owners {CheckPackageHelper.CurrentGitHubUserPlaceholder} to path /sdk/service"));
    }

    [Test]
    public void CheckPackage_ServiceLevelPathEntry_NoPrLabel_ReturnsPathScopedLabelPrompt()
    {
        var customEntries = new List<CodeownersEntry>
        {
            new()
            {
                PathExpression = "/sdk/service/",
                SourceOwners = ["ownerAlice", "ownerBob"],
            }
        };

        var result = helper.CheckPackage(
            "sdk/service/Package.Name",
            "Azure/azure-sdk-for-net",
            customEntries);

        AssertFailure(result, "missing_pr_label", "resolved service-level path entry '/sdk/service' has no PR label");
        Assert.That(result.ResolvedTargetType, Is.EqualTo("path"));
        Assert.That(result.Issues[0].NextStep, Does.Contain("/owners add label \"<pr-label>\" to path /sdk/service"));
    }

    [Test]
    public void CheckPackage_ServiceLabelSupersetDoesNotMatch_ReturnsFailure()
    {
        var result = helper.CheckPackage(
            "sdk/superset-match/Azure.SupersetMatch",
            "Azure/azure-sdk-for-net",
            entries);

        AssertFailure(result, "insufficient_service_owners", "PR label \"MultiLabel1\" has 0 unique service owner(s)");
    }

    [Test]
    public void CheckPackage_MultiplePrLabels_ExactServiceLabelMatch_Passes()
    {
        var result = helper.CheckPackage(
            "sdk/multi-label/Azure.MultiLabel",
            "Azure/azure-sdk-for-net",
            entries);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.PRLabels.Count, Is.EqualTo(2));
        Assert.That(result.ServiceOwners.Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void CheckPackage_ReverseOrderMatching_LastEntryWins()
    {
        var result = helper.CheckPackage(
            "sdk/reverse-test/Azure.ReverseTest",
            "Azure/azure-sdk-for-net",
            entries);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.Owners.Count, Is.EqualTo(3));
        Assert.That(result.PRLabels, Does.Contain("ThreeOwners"));
    }

    [Test]
    public void CheckPackage_ServiceOwnerSearch_LastMatchingEntryWins()
    {
        var customEntries = new List<CodeownersEntry>
        {
            new()
            {
                PathExpression = "/sdk/test/Azure.Test/",
                SourceOwners = ["ownerAlice", "ownerBob"],
                PRLabels = ["TestLabel"],
            },
            new()
            {
                ServiceLabels = ["TestLabel"],
                ServiceOwners = ["serviceOwnerAlice"],
            },
            new()
            {
                ServiceLabels = ["TestLabel"],
                ServiceOwners = ["serviceOwnerAlice", "serviceOwnerBob"],
            }
        };

        var result = helper.CheckPackage(
            "sdk/test/Azure.Test",
            "Azure/azure-sdk-for-net",
            customEntries);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.ServiceOwners, Is.EquivalentTo(new[] { "serviceOwnerAlice", "serviceOwnerBob" }));
    }

    [Test]
    public void CheckPackage_ServiceAttentionLabel_IsIgnoredDuringServiceOwnerMatch()
    {
        var customEntries = CreateEntries(
            sourceOwners: ["ownerAlice", "ownerBob"],
            prLabels: ["TestLabel"],
            serviceOwners: ["serviceOwnerAlice", "serviceOwnerBob"],
            serviceLabels: ["TestLabel", "Service Attention"]);

        var result = helper.CheckPackage(
            "sdk/test/Azure.Test",
            "Azure/azure-sdk-for-net",
            customEntries);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.ServiceOwners, Is.EquivalentTo(new[] { "serviceOwnerAlice", "serviceOwnerBob" }));
        Assert.That(result.ServiceLabels, Is.EquivalentTo(new[] { "TestLabel", "Service Attention" }));
    }

    [Test]
    public void CheckPackage_UnresolvedTeamAliasInSourceOwners_DoesNotCountTowardMinimum_ReturnsFailure()
    {
        var customEntries = CreateEntries(
            sourceOwners: ["ownerAlice", "Azure/unresolved-team"],
            prLabels: ["TestLabel"],
            serviceOwners: ["serviceOwnerAlice", "serviceOwnerBob"]);

        var result = helper.CheckPackage(
            "sdk/test/Azure.Test",
            "Azure/azure-sdk-for-net",
            customEntries);

        AssertFailure(result, "insufficient_owners", "Azure/unresolved-team");
    }

    [Test]
    public void CheckPackage_UnresolvedTeamAliasInServiceOwners_DoesNotCountTowardMinimum_ReturnsFailure()
    {
        var customEntries = CreateEntries(
            sourceOwners: ["ownerAlice", "ownerBob"],
            prLabels: ["TestLabel"],
            serviceOwners: ["serviceOwnerAlice", "Azure/unresolved-team"]);

        var result = helper.CheckPackage(
            "sdk/test/Azure.Test",
            "Azure/azure-sdk-for-net",
            customEntries);

        AssertFailure(result, "insufficient_service_owners", "Azure/unresolved-team");
    }

    [Test]
    public void CheckPackage_UnresolvedTeamAliases_AreFilteredFromSuccessfulResponse()
    {
        var customEntries = CreateEntries(
            sourceOwners: ["ownerAlice", "ownerBob", "Azure/unresolved-team"],
            prLabels: ["TestLabel"],
            serviceOwners: ["serviceOwnerAlice", "serviceOwnerBob", "Azure/unresolved-team"]);

        var result = helper.CheckPackage(
            "sdk/test/Azure.Test",
            "Azure/azure-sdk-for-net",
            customEntries);

        Assert.That(result.OperationStatus, Is.EqualTo(Status.Succeeded));
        Assert.That(result.Owners, Is.EquivalentTo(new[] { "ownerAlice", "ownerBob" }));
        Assert.That(result.ServiceOwners, Is.EquivalentTo(new[] { "serviceOwnerAlice", "serviceOwnerBob" }));
    }

    [Test]
    public void CheckPackage_EmptyEntries_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            helper.CheckPackage(
                "sdk/test/Azure.Test",
                "Azure/azure-sdk-for-net",
                []));

        Assert.That(ex!.Message, Does.Contain("empty"));
    }

    [Test]
    public void CheckPackage_NullDirectoryPath_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            helper.CheckPackage(
                null!,
                "Azure/azure-sdk-for-net",
                entries));
    }

    private static void AssertFailure(CheckPackageResponse response, string issueCode, string messageFragment)
    {
        Assert.That(response.OperationStatus, Is.EqualTo(Status.Failed));
        Assert.That(response.ResponseError, Is.Not.Null.And.Not.Empty);
        Assert.That(response.Issues.Any(issue => issue.Code == issueCode && issue.Message.Contains(messageFragment, StringComparison.Ordinal)),
            Is.True,
            $"Expected issue '{issueCode}' containing '{messageFragment}', but got: {string.Join("; ", response.Issues.Select(issue => $"[{issue.Code}] {issue.Message}"))}");
    }

    private static List<CodeownersEntry> CreateEntries(
        List<string> sourceOwners,
        List<string> prLabels,
        List<string> serviceOwners,
        string pathExpression = "/sdk/test/Azure.Test/",
        List<string>? serviceLabels = null)
    {
        var effectiveServiceLabels = serviceLabels ?? [.. prLabels];

        return
        [
            new CodeownersEntry
            {
                PathExpression = pathExpression,
                SourceOwners = sourceOwners,
                PRLabels = prLabels,
            },
            new CodeownersEntry
            {
                ServiceLabels = effectiveServiceLabels,
                ServiceOwners = serviceOwners,
            }
        ];
    }
}
