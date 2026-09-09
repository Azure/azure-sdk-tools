using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.CodeownersMigration.Conversion;
using NUnit.Framework;

namespace Azure.Sdk.Tools.CodeownersMigration.Tests
{
    public class CodeownersConverterTests
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

        private ConversionResult Convert(string[] lines, Action<ConversionOptions> configure = null)
        {
            string path = Path.Combine(_directory, "CODEOWNERS");
            File.WriteAllLines(path, lines);

            var options = new ConversionOptions
            {
                CodeownersPath = path,
                FragmentSections = new HashSet<string>(new[] { "Client Libraries" }, StringComparer.OrdinalIgnoreCase),
                DefaultSection = "Client Libraries"
            };
            configure?.Invoke(options);

            return new CodeownersConverter(options).Convert();
        }

        private static readonly string[] TwoSectionFile =
        {
            "####################",
            "# Core Libraries",
            "####################",
            "# PRLabel: %Tables",
            "/sdk/tables/    @alice @bob",
            "",
            "####################",
            "# Client Libraries",
            "####################",
            "# PRLabel: %AI Projects",
            "/sdk/ai/    @carol @dave",
            "",
            "# PRLabel: %AI Projects",
            "/sdk/ai/Azure.AI.Projects/    @carol",
            "",
            "# ServiceLabel: %AI Projects",
            "# ServiceOwners: @carol @dave"
        };

        [Test]
        public void MovesFragmentSectionEntriesIntoFragments()
        {
            ConversionResult result = Convert(TwoSectionFile);

            Assert.That(result.Fragments.Keys, Is.EqualTo(new[] { "sdk/ai/owners.yaml" }));

            string fragment = result.Fragments["sdk/ai/owners.yaml"];
            Assert.That(fragment, Does.Contain("- path: ."));
            Assert.That(fragment, Does.Contain("- path: Azure.AI.Projects"));
            Assert.That(fragment, Does.Contain("owners: [carol, dave]"));
        }

        [Test]
        public void LeavesNonFragmentSectionsStatic()
        {
            ConversionResult result = Convert(TwoSectionFile);

            Assert.That(result.ConfigYaml, Does.Contain("- path: /sdk/tables/"));
            Assert.That(result.ConfigYaml, Does.Contain("owners: [alice, bob]"));
        }

        [Test]
        public void RoutesNonDefaultSectionEntriesWithAPerEntryKey()
        {
            string[] file =
            {
                "####################",
                "# Client Libraries",
                "####################",
                "# PRLabel: %Communication",
                "/sdk/communication/Azure.Communication.Chat/    @carol",
                "",
                "####################",
                "# Management Libraries",
                "####################",
                "# PRLabel: %Mgmt",
                "/sdk/communication/Azure.ResourceManager.Communication/    @erin",
                "# PRLabel: %Mgmt",
                "/sdk/compute/Azure.ResourceManager.Compute/    @frank"
            };

            ConversionResult result = Convert(file, o =>
            {
                o.FragmentSections.Add("Management Libraries");
            });

            // A fragment carries no file-level section key; the fragment schema has no such property, so
            // emitting one makes the whole file fail to load. Routing is declared on the entry.
            string mgmt = result.Fragments["sdk/compute/owners.yaml"];
            Assert.That(mgmt, Does.Not.Match(@"(?m)^section:"));
            Assert.That(mgmt, Does.Contain("section: Management Libraries"));

            // One fragment can hold entries from two sections. Tagging the file rather than the entry sent
            // the management entry into Client Libraries, where a broad glob later in the file outranked it.
            string mixed = result.Fragments["sdk/communication/owners.yaml"];
            Assert.That(mixed, Does.Not.Match(@"(?m)^section:"));
            Assert.That(Regex.Matches(mixed, "section: Management Libraries").Count, Is.EqualTo(1));

            string chatBlock = mixed.Substring(mixed.IndexOf("Azure.Communication.Chat/"),
                                               mixed.IndexOf("Azure.ResourceManager.Communication/") - mixed.IndexOf("Azure.Communication.Chat/"));
            Assert.That(chatBlock, Does.Not.Contain("section:"));
        }

        [Test]
        public void KeepsPathlessLabelOwnersInTheConfig()
        {
            ConversionResult result = Convert(TwoSectionFile);

            // A pathless block owns a label, not a directory, so it has no fragment to belong to.
            // Copying it into every fragment that happens to use the label as a PR label produces
            // one duplicate per fragment and no additional information.
            Assert.That(result.ConfigYaml, Does.Contain("- labels: [AI Projects]"));
            Assert.That(result.ConfigYaml, Does.Contain("service-owners: [carol, dave]"));

            string fragment = result.Fragments["sdk/ai/owners.yaml"];
            Assert.That(fragment, Does.Not.Contain("label-owners:"));
        }

        [Test]
        public void SplitsServiceLabelsOffPathBlocksIntoLabelOwners()
        {
            // The fragment schema expresses service ownership only through label-owners, so a legacy block
            // carrying a path and a ServiceLabel has to become two declarations.
            ConversionResult result = Convert(new[]
            {
                "####################",
                "# Client Libraries",
                "####################",
                "# ServiceLabel: %Tables",
                "/sdk/tables/    @alice @bob"
            });

            string fragment = result.Fragments["sdk/tables/owners.yaml"];
            Assert.That(fragment, Does.Contain("- path: ."));
            Assert.That(fragment, Does.Contain("- labels: [Tables]"));
            Assert.That(fragment, Does.Contain("service-owners: [alice, bob]"));
        }

        [Test]
        public void RefusesToMoveAPathAlreadyDefinedStatically()
        {
            // /sdk/tables/ is static in an earlier section. The Client Libraries section renders later, so a
            // fragment definition would silently win. Exact path match only; no glob subset analysis.
            ConversionResult result = Convert(new[]
            {
                "####################",
                "# Core Libraries",
                "####################",
                "/sdk/tables/    @alice @bob",
                "",
                "####################",
                "# Client Libraries",
                "####################",
                "/sdk/tables/    @impostor @other"
            });

            Assert.That(result.Fragments, Is.Empty);
            Assert.That(result.Warnings.Count, Is.EqualTo(1));
            Assert.That(result.Warnings[0], Does.Contain("/sdk/tables/"));
            Assert.That(result.Warnings[0], Does.Contain("also defined statically"));
        }

        [Test]
        public void WarnsAboutPathsThatCannotBecomeFragments()
        {
            ConversionResult result = Convert(new[]
            {
                "####################",
                "# Client Libraries",
                "####################",
                "/sdk/iot*/    @alice @bob"
            });

            Assert.That(result.Fragments, Is.Empty);
            Assert.That(result.Warnings.Single(), Does.Contain("does not fit"));
            Assert.That(result.ConfigYaml, Does.Contain("- path: \"/sdk/iot*/\""));
        }

        [Test]
        public void MarksSectionsProtectedAndDefinedInFiles()
        {
            ConversionResult result = Convert(TwoSectionFile,
                o => o.ProtectedSections = new HashSet<string>(new[] { "Core Libraries" }, StringComparer.OrdinalIgnoreCase));

            Assert.That(result.ConfigYaml, Does.Contain("- name: Core Libraries\n    protected: true"));
            Assert.That(result.ConfigYaml, Does.Contain("- name: Client Libraries\n    defined-in-files: true"));
        }

        [Test]
        public void QuotesPathsThatWouldOtherwiseChangeMeaning()
        {
            ConversionResult result = Convert(new[]
            {
                "####################",
                "# Repository root",
                "####################",
                "/*    @alice @bob",
                "/**/*Management*/    @carol"
            });

            Assert.That(result.ConfigYaml, Does.Contain("- path: \"/**/*Management*/\""));
        }
    }
}
