
using NUnit.Framework;

using Azure.Sdk.Tools.CodeownersUtils.Utils;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using System.Collections.Generic;

namespace Azure.Sdk.Tools.CodeownersUtils.Tests.Utils
{
    [TestFixture]
    internal class CodeownersEntrySorterTests
    {

    #region End-to-End Output Tests

    [Test]
    public void FullOutputTest_SimplePackageEntry()
    {
        // Arrange
        var entry = new CodeownersEntry
        {
            PathExpression = "/sdk/storage/Azure.Storage.Blobs/",
            SourceOwners = ["alice", "bob"],
            OriginalSourceOwners = ["alice", "bob"],
            PRLabels = ["Storage"]
        };

        var entries = new List<CodeownersEntry> { entry };
        CodeownersEntrySorter.SortOwnersInPlace(entries);
        CodeownersEntrySorter.SortLabelsInPlace(entries);

        var formatted = entries[0].FormatCodeownersEntry();

        // Assert expected format
        var expectedLines = new[]
        {
            "# PRLabel: %Storage",
            "/sdk/storage/Azure.Storage.Blobs/    @alice @bob"
        };

        foreach (var line in expectedLines)
        {
            Assert.That(formatted, Does.Contain(line));
        }
    }

    [Test]
    public void FullOutputTest_ComplexEntry()
    {
        // Arrange: Entry with all metadata
        var entry = new CodeownersEntry
        {
            PathExpression = "/sdk/core/Azure.Core/",
            SourceOwners = ["coredev2", "coredev1"],
            OriginalSourceOwners = ["coredev2", "coredev1"],
            PRLabels = ["Core", "Azure"],
            ServiceLabels = ["Core"],
            ServiceOwners = ["svcowner"],
            OriginalServiceOwners = ["svcowner"],
            AzureSdkOwners = ["sdkowner"],
            OriginalAzureSdkOwners = ["sdkowner"]
        };

        var entries = new List<CodeownersEntry> { entry };
        CodeownersEntrySorter.SortOwnersInPlace(entries);
        CodeownersEntrySorter.SortLabelsInPlace(entries);

        var formatted = entries[0].FormatCodeownersEntry();

        // Assert
        // Note: For pathed entries, ServiceOwners are NOT included (they're inferred from source owners)
        Assert.Multiple(() =>
        {
            Assert.That(formatted, Does.Contain("# PRLabel: %Azure %Core"));
            Assert.That(formatted, Does.Contain("/sdk/core/Azure.Core/    @coredev1 @coredev2"));
            Assert.That(formatted, Does.Contain("# AzureSdkOwners: @sdkowner"));
            Assert.That(formatted, Does.Contain("# ServiceLabel: %Core"));
            // ServiceOwners is NOT in pathed entries - it's inferred from source owners
            Assert.That(formatted, Does.Not.Contain("# ServiceOwners:"));
        });
    }

    [Test]
    public void FullOutputTest_PathlessEntry()
    {
        // Arrange: Pathless entry for triage
        var entry = new CodeownersEntry
        {
            PathExpression = "",
            ServiceLabels = ["Storage", "Analytics"],
            ServiceOwners = ["storageservice"],
            OriginalServiceOwners = ["storageservice"]
        };

        var entries = new List<CodeownersEntry> { entry };
        CodeownersEntrySorter.SortLabelsInPlace(entries);

        var formatted = entries[0].FormatCodeownersEntry();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(formatted, Does.Contain("# ServiceLabel: %Analytics %Storage"));
            Assert.That(formatted, Does.Contain("# ServiceOwners: @storageservice"));
            // Should NOT contain a path line
            Assert.That(formatted, Does.Not.Contain("/sdk/"));
        });
    }

    #endregion
        
    }
}
