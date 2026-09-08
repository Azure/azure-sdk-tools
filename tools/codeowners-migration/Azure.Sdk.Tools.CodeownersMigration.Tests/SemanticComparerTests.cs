using System.Collections.Generic;
using System.IO;
using System.Linq;
using Azure.Sdk.Tools.CodeownersMigration.Verification;
using NUnit.Framework;

namespace Azure.Sdk.Tools.CodeownersMigration.Tests
{
    /// <summary>
    /// Fixtures deliberately use individual owners only. Team aliases would make the parser expand membership
    /// from blob storage, which would make these tests depend on the network.
    /// </summary>
    public class SemanticComparerTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "codeowners-migration-tests", Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        private CodeownersDocument Write(string name, params string[] lines)
        {
            string path = Path.Combine(_directory, name);
            File.WriteAllLines(path, lines);
            return CodeownersDocument.Load(path);
        }

        [Test]
        public void IdenticalFilesAreEquivalent()
        {
            string[] content =
            {
                "# PRLabel: %Tables",
                "/sdk/tables/    @alice @bob",
                "",
                "# ServiceLabel: %Tables",
                "# ServiceOwners: @alice @bob"
            };

            ComparisonReport report = SemanticComparer.Compare(Write("a", content), Write("b", content), null);

            Assert.That(report.IsEquivalent, Is.True, string.Join("\n", report.Differences));
        }

        [Test]
        public void CommentsAndBlankLinesAreIgnored()
        {
            CodeownersDocument baseline = Write("a",
                "# a helpful note",
                "# PRLabel: %Tables",
                "/sdk/tables/    @alice @bob");

            CodeownersDocument candidate = Write("b",
                "",
                "# Sources: sdk/tables/owners.yaml",
                "# PRLabel: %Tables",
                "/sdk/tables/    @alice @bob",
                "");

            ComparisonReport report = SemanticComparer.Compare(baseline, candidate, null);

            Assert.That(report.IsEquivalent, Is.True, string.Join("\n", report.Differences));
        }

        [Test]
        public void OwnerOrderAndCaseAreIgnored()
        {
            ComparisonReport report = SemanticComparer.Compare(
                Write("a", "/sdk/tables/    @alice @bob"),
                Write("b", "/sdk/tables/    @Bob @Alice"),
                null);

            Assert.That(report.IsEquivalent, Is.True, string.Join("\n", report.Differences));
        }

        [Test]
        public void DetectsChangedOwners()
        {
            ComparisonReport report = SemanticComparer.Compare(
                Write("a", "/sdk/tables/    @alice @bob"),
                Write("b", "/sdk/tables/    @alice @carol"),
                null);

            Assert.That(report.Differences.Select(d => d.Kind), Does.Contain(DifferenceKind.PathDeclarationChanged));
        }

        [Test]
        public void DetectsRemovedAndAddedPaths()
        {
            ComparisonReport report = SemanticComparer.Compare(
                Write("a", "/sdk/tables/    @alice @bob"),
                Write("b", "/sdk/queues/    @alice @bob"),
                null);

            Assert.That(report.Differences.Select(d => d.Kind),
                        Is.EquivalentTo(new[] { DifferenceKind.PathOnlyInBaseline, DifferenceKind.PathOnlyInCandidate }));
        }

        [Test]
        public void UnionsLabelOwnersAcrossBlocks()
        {
            // Two source blocks declaring the same label set must compare equal to one merged block. This is
            // the union rule that lets several owners.yaml files contribute to a single label.
            CodeownersDocument baseline = Write("a",
                "# ServiceLabel: %AI Projects",
                "# ServiceOwners: @alice",
                "",
                "# ServiceLabel: %AI Projects",
                "# ServiceOwners: @bob");

            CodeownersDocument candidate = Write("b",
                "# ServiceLabel: %AI Projects",
                "# ServiceOwners: @alice @bob");

            ComparisonReport report = SemanticComparer.Compare(baseline, candidate, null);

            Assert.That(report.IsEquivalent, Is.True, string.Join("\n", report.Differences));
        }

        [Test]
        public void LabelSetsDifferingByOneLabelAreDistinct()
        {
            ComparisonReport report = SemanticComparer.Compare(
                Write("a", "# ServiceLabel: %ARM %Mgmt", "# ServiceOwners: @alice"),
                Write("b", "# ServiceLabel: %ARM", "# ServiceOwners: @alice"),
                null);

            Assert.That(report.Differences.Select(d => d.Kind),
                        Is.EquivalentTo(new[] { DifferenceKind.LabelSetOnlyInBaseline, DifferenceKind.LabelSetOnlyInCandidate }));
        }

        [Test]
        public void ServiceOwnersInheritedFromSourceOwnersAreCompared()
        {
            // A block ending in a source path line with a ServiceLabel takes its service owners from the source
            // owners. The equivalent explicit form must compare equal.
            CodeownersDocument baseline = Write("a",
                "# ServiceLabel: %Tables",
                "/sdk/tables/    @alice @bob");

            CodeownersDocument candidate = Write("b",
                "/sdk/tables/    @alice @bob",
                "",
                "# ServiceLabel: %Tables",
                "# ServiceOwners: @alice @bob");

            ComparisonReport report = SemanticComparer.Compare(baseline, candidate, null);

            Assert.That(report.IsEquivalent, Is.True, string.Join("\n", report.Differences));
        }

        [Test]
        public void ReorderingIsOnlyDetectedThroughResolution()
        {
            CodeownersDocument baseline = Write("a",
                "/sdk/**/ci.yml    @alice",
                "/sdk/tables/    @bob");

            CodeownersDocument candidate = Write("b",
                "/sdk/tables/    @bob",
                "/sdk/**/ci.yml    @alice");

            // Declaration and label passes see identical data.
            Assert.That(SemanticComparer.Compare(baseline, candidate, null).IsEquivalent, Is.True);

            // Resolution shows that a different entry now wins.
            ComparisonReport report = SemanticComparer.Compare(baseline, candidate, new[] { "/sdk/tables/ci.yml" });

            Assert.That(report.Differences.Select(d => d.Kind), Does.Contain(DifferenceKind.ResolvedOwnersChanged));
        }

        [Test]
        public void ReportsBlocksTheParserRejected()
        {
            // A ServiceLabel with no owner moniker is an invalid block; the parser drops it. That is silent data
            // loss, so it must surface as a difference rather than as a missing entry.
            CodeownersDocument candidate = Write("b", "# ServiceLabel: %Tables", "");

            Assert.That(candidate.ParseDiagnostics, Is.Not.Empty);

            ComparisonReport report = SemanticComparer.Compare(Write("a", "/sdk/tables/    @alice"), candidate, null);

            Assert.That(report.Differences.Select(d => d.Kind), Does.Contain(DifferenceKind.ParseError));
        }
    }
}
