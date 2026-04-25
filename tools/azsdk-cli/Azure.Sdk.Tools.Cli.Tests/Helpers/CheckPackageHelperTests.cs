// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class CheckPackageHelperTests
{
    private List<CodeownersEntry> entries;

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

    [Test]
    public void CheckPackage_TwoOwners_ValidServiceOwners_Passes()
    {
        var helper = new CheckPackageHelper();
        var ownersConfig = new OwnersConfig();

        var result = helper.CheckPackage(
            "sdk/two-owners/Azure.TwoOwners",
            entries,
            ownersConfig);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.DirectoryPath, Is.EqualTo("sdk/two-owners/Azure.TwoOwners"));
        Assert.That(result.Owners.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(result.PRLabels, Does.Contain("TwoOwners"));
        Assert.That(result.ServiceOwners.Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void CheckPackage_ThreeOwners_ValidServiceOwners_Passes()
    {
        var helper = new CheckPackageHelper();
        var ownersConfig = new OwnersConfig();

        var result = helper.CheckPackage(
            "sdk/three-owners/Azure.ThreeOwners",
            entries,
            ownersConfig);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Owners.Count, Is.EqualTo(3));
        Assert.That(result.ServiceOwners.Count, Is.GreaterThanOrEqualTo(2));
    }

    [TestCase("/sdk/test/")]
    [TestCase("/sdk/test/Azure.Test")]
    public void CheckPackage_SkipGateMatch_PassesWithoutFurtherValidation(string skipGate)
    {
        var helper = new CheckPackageHelper();
        var ownersConfig = new OwnersConfig
        {
            SkipGates = [skipGate]
        };

        var result = helper.CheckPackage(
            "sdk/test/Azure.Test",
            CreateEntries(
                sourceOwners: ["ownerAlice"],
                serviceOwners: ["serviceOwnerAlice"]),
            ownersConfig);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.DirectoryPath, Is.EqualTo("sdk/test/Azure.Test"));
        Assert.That(result.Skipped, Is.True);
        Assert.That(result.Details, Does.Contain(skipGate));
        Assert.That(result.Owners, Is.Empty);
        Assert.That(result.PRLabels, Is.Empty);
        Assert.That(result.ServiceOwners, Is.Empty);
    }

    [Test]
    public void CheckPackage_EmptyOwnersConfig_DoesNotSkipValidation()
    {
        var helper = new CheckPackageHelper();
        var ownersConfig = new OwnersConfig();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            helper.CheckPackage(
                "sdk/test/Azure.Test",
                CreateEntries(
                    sourceOwners: ["ownerAlice"],
                    serviceOwners: ["serviceOwnerAlice", "serviceOwnerBob"]),
                ownersConfig));

        Assert.That(ex.Message, Does.Contain("check-package failed"));
        Assert.That(ex.Message, Does.Contain("1 unique owner"));
    }

    [Test]
    public void CheckPackage_OneOwner_Throws()
    {
        var helper = new CheckPackageHelper();
        var ownersConfig = new OwnersConfig();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            helper.CheckPackage(
                "sdk/one-owner/Azure.OneOwner",
                entries,
                ownersConfig));

        Assert.That(ex.Message, Does.Contain("check-package failed"));
        Assert.That(ex.Message, Does.Contain("1 unique owner"));
        Assert.That(ex.Message, Does.Contain("at least 2"));
    }

    [Test]
    public void CheckPackage_NoPrLabels_Throws()
    {
        var helper = new CheckPackageHelper();
        var ownersConfig = new OwnersConfig();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            helper.CheckPackage(
                "sdk/no-labels/Azure.NoLabels",
                entries,
                ownersConfig));

        Assert.That(ex.Message, Does.Contain("check-package failed"));
        Assert.That(ex.Message, Does.Contain("No PR labels"));
    }

    [Test]
    public void CheckPackage_ZeroServiceOwners_Throws()
    {
        var helper = new CheckPackageHelper();
        var ownersConfig = new OwnersConfig();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            helper.CheckPackage(
                "sdk/zero-svc-owners/Azure.ZeroSvcOwners",
                entries,
                ownersConfig));

        Assert.That(ex.Message, Does.Contain("check-package failed"));
        Assert.That(ex.Message, Does.Contain("service owner"));
    }

    [Test]
    public void CheckPackage_NoMatchingServiceLabel_Throws()
    {
        var helper = new CheckPackageHelper();
        var ownersConfig = new OwnersConfig();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            helper.CheckPackage(
                "sdk/no-svc-match/Azure.NoSvcMatch",
                entries,
                ownersConfig));

        Assert.That(ex.Message, Does.Contain("check-package failed"));
        Assert.That(ex.Message, Does.Contain("No service label entry found"));
    }

    [Test]
    public void CheckPackage_InsufficientServiceOwners_Throws()
    {
        var helper = new CheckPackageHelper();
        var ownersConfig = new OwnersConfig();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            helper.CheckPackage(
                "sdk/insufficient-svc/Azure.InsufficientSvc",
                entries,
                ownersConfig));

        Assert.That(ex.Message, Does.Contain("check-package failed"));
        Assert.That(ex.Message, Does.Contain("1 unique service owner"));
    }

    [Test]
    public void CheckPackage_NoMatchingPath_Throws()
    {
        var helper = new CheckPackageHelper();
        var ownersConfig = new OwnersConfig();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            helper.CheckPackage(
                "sdk/does-not-exist/Azure.NonExistent",
                entries,
                ownersConfig));

        Assert.That(ex.Message, Does.Contain("check-package failed"));
        Assert.That(ex.Message, Does.Contain("No CODEOWNERS entry matches path"));
    }

    [Test]
    public void CheckPackage_ServiceLabelSupersetDoesNotMatch_Throws()
    {
        var helper = new CheckPackageHelper();
        var ownersConfig = new OwnersConfig();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            helper.CheckPackage(
                "sdk/superset-match/Azure.SupersetMatch",
                entries,
                ownersConfig));

        Assert.That(ex.Message, Does.Contain("check-package failed"));
        Assert.That(ex.Message, Does.Contain("No service label entry found"));
    }

    [Test]
    public void CheckPackage_MultiplePrLabels_ExactServiceLabelMatch_Passes()
    {
        var helper = new CheckPackageHelper();
        var ownersConfig = new OwnersConfig();

        var result = helper.CheckPackage(
            "sdk/multi-label/Azure.MultiLabel",
            entries,
            ownersConfig);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.PRLabels.Count, Is.EqualTo(2));
        Assert.That(result.ServiceOwners.Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void CheckPackage_ReverseOrderMatching_LastEntryWins()
    {
        var helper = new CheckPackageHelper();
        var ownersConfig = new OwnersConfig();

        var result = helper.CheckPackage(
            "sdk/reverse-test/Azure.ReverseTest",
            entries,
            ownersConfig);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Owners.Count, Is.EqualTo(3));
        Assert.That(result.PRLabels, Does.Contain("ThreeOwners"));
        Assert.That(result.ServiceOwners.Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void CheckPackage_ServiceOwners_ThreeOwners_Passes()
    {
        var helper = new CheckPackageHelper();
        var ownersConfig = new OwnersConfig();

        var result = helper.CheckPackage(
            "sdk/three-owners/Azure.ThreeOwners",
            entries,
            ownersConfig);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.ServiceOwners.Count, Is.EqualTo(3));
    }

    [Test]
    public void CheckPackage_ServiceOwnerSearch_LastMatchingEntryWins()
    {
        var helper = new CheckPackageHelper();
        var ownersConfig = new OwnersConfig();
        var entries = new List<CodeownersEntry>
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
            entries,
            ownersConfig);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.ServiceOwners, Is.EquivalentTo(new[] { "serviceOwnerAlice", "serviceOwnerBob" }));
    }

    [Test]
    public void CheckPackage_ServiceAttentionLabel_IsIgnoredDuringServiceOwnerMatch()
    {
        var helper = new CheckPackageHelper();
        var ownersConfig = new OwnersConfig();
        var entries = CreateEntries(
            sourceOwners: ["ownerAlice", "ownerBob"],
            serviceOwners: ["serviceOwnerAlice", "serviceOwnerBob"],
            serviceLabels: ["TestLabel", "Service Attention"]);

        var result = helper.CheckPackage(
            "sdk/test/Azure.Test",
            entries,
            ownersConfig);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.ServiceOwners, Is.EquivalentTo(new[] { "serviceOwnerAlice", "serviceOwnerBob" }));
        Assert.That(result.ServiceLabels, Is.EquivalentTo(new[] { "TestLabel", "Service Attention" }));
    }

    [Test]
    public void CheckPackage_UnresolvedTeamAliasInSourceOwners_DoesNotCountTowardMinimum_Throws()
    {
        var helper = new CheckPackageHelper();
        var ownersConfig = new OwnersConfig();
        var entries = CreateEntries(
            sourceOwners: ["ownerAlice", "Azure/unresolved-team"],
            serviceOwners: ["serviceOwnerAlice", "serviceOwnerBob"]);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            helper.CheckPackage(
                "sdk/test/Azure.Test",
                entries,
                ownersConfig));

        Assert.That(ex.Message, Does.Contain("check-package failed"));
        Assert.That(ex.Message, Does.Contain("1 unique owner"));
        Assert.That(ex.Message, Does.Contain("Azure/unresolved-team"));
    }

    [Test]
    public void CheckPackage_UnresolvedTeamAliasInServiceOwners_DoesNotCountTowardMinimum_Throws()
    {
        var helper = new CheckPackageHelper();
        var ownersConfig = new OwnersConfig();
        var entries = CreateEntries(
            sourceOwners: ["ownerAlice", "ownerBob"],
            serviceOwners: ["serviceOwnerAlice", "Azure/unresolved-team"]);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            helper.CheckPackage(
                "sdk/test/Azure.Test",
                entries,
                ownersConfig));

        Assert.That(ex.Message, Does.Contain("check-package failed"));
        Assert.That(ex.Message, Does.Contain("1 unique service owner"));
        Assert.That(ex.Message, Does.Contain("Azure/unresolved-team"));
    }

    [Test]
    public void CheckPackage_UnresolvedTeamAliases_AreFilteredFromSuccessfulResponse()
    {
        var helper = new CheckPackageHelper();
        var ownersConfig = new OwnersConfig();
        var entries = CreateEntries(
            sourceOwners: ["ownerAlice", "ownerBob", "Azure/unresolved-team"],
            serviceOwners: ["serviceOwnerAlice", "serviceOwnerBob", "Azure/unresolved-team"]);

        var result = helper.CheckPackage(
            "sdk/test/Azure.Test",
            entries,
            ownersConfig);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Owners, Is.EquivalentTo(new[] { "ownerAlice", "ownerBob" }));
        Assert.That(result.ServiceOwners, Is.EquivalentTo(new[] { "serviceOwnerAlice", "serviceOwnerBob" }));
    }

    [Test]
    public void CheckPackage_EmptyEntries_Throws()
    {
        var helper = new CheckPackageHelper();
        var ownersConfig = new OwnersConfig();

        var ex = Assert.Throws<ArgumentException>(() =>
            helper.CheckPackage(
                "sdk/test",
                new List<CodeownersEntry>(),
                ownersConfig));

        Assert.That(ex.Message, Does.Contain("empty"));
    }

    [Test]
    public void CheckPackage_NullDirectoryPath_Throws()
    {
        var helper = new CheckPackageHelper();
        var ownersConfig = new OwnersConfig();

        Assert.Throws<ArgumentException>(() =>
            helper.CheckPackage(null!, entries, ownersConfig));
    }

    private static List<CodeownersEntry> CreateEntries(
        List<string> sourceOwners,
        List<string> serviceOwners,
        List<string>? serviceLabels = null)
    {
        var effectiveServiceLabels = serviceLabels ?? new List<string> { "TestLabel" };

        return
        [
            new CodeownersEntry
            {
                PathExpression = "/sdk/test/Azure.Test/",
                SourceOwners = sourceOwners,
                PRLabels = ["TestLabel"],
            },
            new CodeownersEntry
            {
                ServiceLabels = effectiveServiceLabels,
                ServiceOwners = serviceOwners,
            }
        ];
    }

}
