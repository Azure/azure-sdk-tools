// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class CheckPackageHelperTests
{
    private CheckPackageHelper _helper;
    private List<CodeownersEntry> _entries;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var fixturePath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "TestAssets",
            "check-package-test-codeowners.txt");

        Assert.That(File.Exists(fixturePath), Is.True,
            $"Test fixture not found at {fixturePath}");

        _entries = CodeownersParser.ParseCodeownersFile(fixturePath);
    }

    [SetUp]
    public void SetUp()
    {
        _helper = new CheckPackageHelper();
    }

    [Test]
    public void CheckPackage_TwoOwners_ValidServiceOwners_Passes()
    {
        var result = _helper.CheckPackage(
            "sdk/two-owners/Azure.TwoOwners",
            _entries);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.DirectoryPath, Is.EqualTo("sdk/two-owners/Azure.TwoOwners"));
        Assert.That(result.Owners.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(result.PRLabels, Does.Contain("TwoOwners"));
        Assert.That(result.ServiceOwners.Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void CheckPackage_ThreeOwners_ValidServiceOwners_Passes()
    {
        var result = _helper.CheckPackage(
            "sdk/three-owners/Azure.ThreeOwners",
            _entries);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Owners.Count, Is.EqualTo(3));
        Assert.That(result.ServiceOwners.Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void CheckPackage_OneOwner_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _helper.CheckPackage(
                "sdk/one-owner/Azure.OneOwner",
                _entries));

        Assert.That(ex.Message, Does.Contain("check-package failed"));
        Assert.That(ex.Message, Does.Contain("1 unique owner"));
        Assert.That(ex.Message, Does.Contain("at least 2"));
    }

    [Test]
    public void CheckPackage_NoPrLabels_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _helper.CheckPackage(
                "sdk/no-labels/Azure.NoLabels",
                _entries));

        Assert.That(ex.Message, Does.Contain("check-package failed"));
        Assert.That(ex.Message, Does.Contain("No PR labels"));
    }

    [Test]
    public void CheckPackage_ZeroServiceOwners_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _helper.CheckPackage(
                "sdk/zero-svc-owners/Azure.ZeroSvcOwners",
                _entries));

        Assert.That(ex.Message, Does.Contain("check-package failed"));
        Assert.That(ex.Message, Does.Contain("service owner"));
    }

    [Test]
    public void CheckPackage_NoMatchingServiceLabel_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _helper.CheckPackage(
                "sdk/no-svc-match/Azure.NoSvcMatch",
                _entries));

        Assert.That(ex.Message, Does.Contain("check-package failed"));
        Assert.That(ex.Message, Does.Contain("No service label entry found"));
    }

    [Test]
    public void CheckPackage_InsufficientServiceOwners_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _helper.CheckPackage(
                "sdk/insufficient-svc/Azure.InsufficientSvc",
                _entries));

        Assert.That(ex.Message, Does.Contain("check-package failed"));
        Assert.That(ex.Message, Does.Contain("1 unique service owner"));
    }

    [Test]
    public void CheckPackage_NoMatchingPath_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _helper.CheckPackage(
                "sdk/does-not-exist/Azure.NonExistent",
                _entries));

        Assert.That(ex.Message, Does.Contain("check-package failed"));
        Assert.That(ex.Message, Does.Contain("No CODEOWNERS entry matches path"));
    }

    [Test]
    public void CheckPackage_ServiceLabelSupersetDoesNotMatch_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _helper.CheckPackage(
                "sdk/superset-match/Azure.SupersetMatch",
                _entries));

        Assert.That(ex.Message, Does.Contain("check-package failed"));
        Assert.That(ex.Message, Does.Contain("No service label entry found"));
    }

    [Test]
    public void CheckPackage_MultiplePrLabels_ExactServiceLabelMatch_Passes()
    {
        var result = _helper.CheckPackage(
            "sdk/multi-label/Azure.MultiLabel",
            _entries);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.PRLabels.Count, Is.EqualTo(2));
        Assert.That(result.ServiceOwners.Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void CheckPackage_ReverseOrderMatching_LastEntryWins()
    {
        var result = _helper.CheckPackage(
            "sdk/reverse-test/Azure.ReverseTest",
            _entries);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Owners.Count, Is.EqualTo(3));
        Assert.That(result.PRLabels, Does.Contain("ThreeOwners"));
        Assert.That(result.ServiceOwners.Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void CheckPackage_ServiceOwners_ThreeOwners_Passes()
    {
        var result = _helper.CheckPackage(
            "sdk/three-owners/Azure.ThreeOwners",
            _entries);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.ServiceOwners.Count, Is.EqualTo(3));
    }

    [Test]
    public void CheckPackage_ServiceOwnerSearch_LastMatchingEntryWins()
    {
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

        var result = _helper.CheckPackage(
            "sdk/test/Azure.Test",
            entries);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.ServiceOwners, Is.EquivalentTo(new[] { "serviceOwnerAlice", "serviceOwnerBob" }));
    }

    [Test]
    public void CheckPackage_ServiceAttentionLabel_IsIgnoredDuringServiceOwnerMatch()
    {
        var entries = CreateEntries(
            sourceOwners: ["ownerAlice", "ownerBob"],
            serviceOwners: ["serviceOwnerAlice", "serviceOwnerBob"],
            serviceLabels: ["TestLabel", "Service Attention"]);

        var result = _helper.CheckPackage(
            "sdk/test/Azure.Test",
            entries);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.ServiceOwners, Is.EquivalentTo(new[] { "serviceOwnerAlice", "serviceOwnerBob" }));
        Assert.That(result.ServiceLabels, Is.EquivalentTo(new[] { "TestLabel", "Service Attention" }));
    }

    [Test]
    public void CheckPackage_UnresolvedTeamAliasInSourceOwners_DoesNotCountTowardMinimum_Throws()
    {
        var entries = CreateEntries(
            sourceOwners: ["ownerAlice", "Azure/unresolved-team"],
            serviceOwners: ["serviceOwnerAlice", "serviceOwnerBob"]);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            _helper.CheckPackage(
                "sdk/test/Azure.Test",
                entries));

        Assert.That(ex.Message, Does.Contain("check-package failed"));
        Assert.That(ex.Message, Does.Contain("1 unique owner"));
        Assert.That(ex.Message, Does.Contain("Azure/unresolved-team"));
    }

    [Test]
    public void CheckPackage_UnresolvedTeamAliasInServiceOwners_DoesNotCountTowardMinimum_Throws()
    {
        var entries = CreateEntries(
            sourceOwners: ["ownerAlice", "ownerBob"],
            serviceOwners: ["serviceOwnerAlice", "Azure/unresolved-team"]);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            _helper.CheckPackage(
                "sdk/test/Azure.Test",
                entries));

        Assert.That(ex.Message, Does.Contain("check-package failed"));
        Assert.That(ex.Message, Does.Contain("1 unique service owner"));
        Assert.That(ex.Message, Does.Contain("Azure/unresolved-team"));
    }

    [Test]
    public void CheckPackage_UnresolvedTeamAliases_AreFilteredFromSuccessfulResponse()
    {
        var entries = CreateEntries(
            sourceOwners: ["ownerAlice", "ownerBob", "Azure/unresolved-team"],
            serviceOwners: ["serviceOwnerAlice", "serviceOwnerBob", "Azure/unresolved-team"]);

        var result = _helper.CheckPackage(
            "sdk/test/Azure.Test",
            entries);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Owners, Is.EquivalentTo(new[] { "ownerAlice", "ownerBob" }));
        Assert.That(result.ServiceOwners, Is.EquivalentTo(new[] { "serviceOwnerAlice", "serviceOwnerBob" }));
    }

    [Test]
    public void CheckPackage_EmptyEntries_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            _helper.CheckPackage(
                "sdk/test",
                new List<CodeownersEntry>()));

        Assert.That(ex.Message, Does.Contain("empty"));
    }

    [Test]
    public void CheckPackage_NullDirectoryPath_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            _helper.CheckPackage(null!, _entries));
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
