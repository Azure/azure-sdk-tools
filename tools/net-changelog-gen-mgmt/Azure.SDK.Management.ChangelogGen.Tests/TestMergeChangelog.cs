// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.SDK.ChangelogGen.Compare;
using Azure.SDK.ChangelogGen.Report;

namespace Azure.SDK.ChangelogGen.Tests
{
    [TestClass]
    public class TestMergeChangeLog
    {
        private const string ReleaseVersion = "1.2.3";
        private const string ReleaseDate = "2099-09-10";
        private const string CurrentMetadata = """
            { "apiVersions": { "Microsoft.ApiCenter": "2024-06-01-preview" } }
            """;

        [TestMethod]
        public void TestMergeWithExistingNote()
        {
            string content1 = File.ReadAllText("apiFile1.cs.txt");
            string content2 = File.ReadAllText("apiFile2.cs.txt");
            ChangeLogResult r = new ChangeLogResult
            {
                ApiChange = Program.CompareApi(content2, content1),
                AzureCoreVersionChange = new StringValueChange("1.1.0", "1.0.1", "Azure Core upgraded"),
                AzureResourceManagerVersionChange = new StringValueChange("1.2.0", "1.0.2", "Azure RM upgraded"),
                SpecVersionChanges = new List<StringValueChange>
                {
                    new StringValueChange("2020-01-01", "2030-01-01", "spec upgraded")
                }
            };

            Release newRelease = r.GenerateReleaseNote(ReleaseVersion, ReleaseDate, new List<ChangeCatogory>() { ChangeCatogory.Obsoleted });

            var releases = Release.FromChangelog(File.ReadAllText("changelog1.md"));

            newRelease.MergeTo(releases[0], MergeMode.Group);

            var mergedChangelog = Release.ToChangeLog(releases);
            var baseline = File.ReadAllText("mergedChangelog1.md");
            Assert.AreEqual(baseline.Replace("\r\n", "\n").TrimEnd('\r', '\n'), mergedChangelog.Replace("\r\n", "\n"));
        }

        [TestMethod]
        public void TestChangelogWhenTypeSpecApiVersionChanges()
        {
            const string baselineMetadata = """
                { "apiVersions": { "Microsoft.ApiCenter": "2023-04-01" } }
                """;
            string expected = $$"""
                ## {{ReleaseVersion}} ({{ReleaseDate}})

                ### Features Added

                - Upgraded Microsoft.ApiCenter from 2023-04-01 to 2024-06-01-preview
                """;

            string changelog = GenerateTypeSpecReleaseNote(baselineMetadata);

            Assert.AreEqual(expected, changelog.Replace("\r\n", "\n"));
        }

        [TestMethod]
        public void TestChangelogWhenMultipleTypeSpecApiVersionsChange()
        {
            const string baselineMetadata = """
                {
                    "apiVersions": {
                        "Microsoft.ApiCenter": "2023-04-01",
                        "Microsoft.Compute": "2023-07-01-preview"
                    }
                }
                """;
            const string currentMetadata = """
                {
                    "apiVersions": {
                        "Microsoft.Compute": "2024-03-01",
                        "Microsoft.ApiCenter": "2024-06-01-preview"
                    }
                }
                """;
            string expected = $$"""
                ## {{ReleaseVersion}} ({{ReleaseDate}})

                ### Features Added

                - Upgraded Microsoft.ApiCenter from 2023-04-01 to 2024-06-01-preview
                - Upgraded Microsoft.Compute from 2023-07-01-preview to 2024-03-01
                """;
            ChangeLogResult result = new ChangeLogResult
            {
                SpecVersionChanges = Program.CompareApiVersions(currentMetadata, baselineMetadata)
            };

            string changelog = result.GenerateReleaseNote(
                ReleaseVersion,
                ReleaseDate,
                new List<ChangeCatogory>()).ToString();

            Assert.AreEqual(expected, changelog.Replace("\r\n", "\n"));
        }

        [TestMethod]
        public void TestChangelogWhenTypeSpecApiVersionIsUnchanged()
        {
            string changelog = GenerateTypeSpecReleaseNote(CurrentMetadata);

            Assert.AreEqual($"## {ReleaseVersion} ({ReleaseDate})", changelog);
        }

        [TestMethod]
        public void TestChangelogWhenBaselineMetadataIsMissing()
        {
            string changelog = GenerateTypeSpecReleaseNote(null);

            Assert.AreEqual($"## {ReleaseVersion} ({ReleaseDate})", changelog);
        }

        [TestMethod]
        public void TestChangelogWhenBaselineMetadataIsMissingAndAzureCoreChanges()
        {
            string expected = $$"""
                ## {{ReleaseVersion}} ({{ReleaseDate}})

                ### Other Changes

                - Upgraded Azure.Core from 1.0.0 to 1.1.0
                """;
            StringValueChange azureCoreChange = new StringValueChange(
                "1.1.0",
                "1.0.0",
                "Upgraded Azure.Core from 1.0.0 to 1.1.0");

            string changelog = GenerateTypeSpecReleaseNote(null, azureCoreChange);

            Assert.AreEqual(expected, changelog.Replace("\r\n", "\n"));
        }

        [TestMethod]
        public void TestChangelogWhenBaselineMetadataIsMissingAndAzureResourceManagerChanges()
        {
            string expected = $$"""
                ## {{ReleaseVersion}} ({{ReleaseDate}})

                ### Other Changes

                - Upgraded Azure.ResourceManager from 1.0.0 to 1.1.0
                """;
            StringValueChange azureResourceManagerChange = new StringValueChange(
                "1.1.0",
                "1.0.0",
                "Upgraded Azure.ResourceManager from 1.0.0 to 1.1.0");

            string changelog = GenerateTypeSpecReleaseNote(
                null,
                azureResourceManagerVersionChange: azureResourceManagerChange);

            Assert.AreEqual(expected, changelog.Replace("\r\n", "\n"));
        }

        [TestMethod]
        public void TestChangelogWhenBaselineMetadataIsMissingAndApiChanges()
        {
            string baselineApi = File.ReadAllText("apiFile1.cs.txt").Replace("\r\n", "\n");
            string currentApi = baselineApi.Replace(
                "public partial class TestMethod\n    {",
                "public partial class TestMethod\n    {\n        public string GetName() { throw null; }");
            string expected = $$"""
                ## {{ReleaseVersion}} ({{ReleaseDate}})

                ### Other Changes

                - Added method 'String GetName()' in type Azure.ResourceManager.AppService.TestMethod
                """;
            ChangeLogResult result = new ChangeLogResult
            {
                SpecVersionChanges = Program.CompareApiVersions(CurrentMetadata, null),
                ApiChange = Program.CompareApi(currentApi, baselineApi)
            };

            string changelog = result.GenerateReleaseNote(
                ReleaseVersion,
                ReleaseDate,
                new List<ChangeCatogory> { ChangeCatogory.Added }).ToString();

            Assert.AreEqual(expected, changelog.Replace("\r\n", "\n"));
        }

        private static string GenerateTypeSpecReleaseNote(
            string? baselineMetadata,
            StringValueChange? azureCoreVersionChange = null,
            StringValueChange? azureResourceManagerVersionChange = null)
        {
            ChangeLogResult result = new ChangeLogResult
            {
                SpecVersionChanges = Program.CompareApiVersions(CurrentMetadata, baselineMetadata),
                AzureCoreVersionChange = azureCoreVersionChange,
                AzureResourceManagerVersionChange = azureResourceManagerVersionChange
            };

            return result.GenerateReleaseNote(ReleaseVersion, ReleaseDate, new List<ChangeCatogory>()).ToString();
        }
    }

}
