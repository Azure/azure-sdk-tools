// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.SDK.ChangelogGen.Compare;
using Azure.SDK.ChangelogGen.Report;

namespace Azure.SDK.ChangelogGen.Tests
{
    [TestClass]
    public class TestMergeChangeLog
    {
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
                SpecVersionChange = new StringValueChange("2020-01-01", "2030-01-01", "spec upgraded")
            };

            Release newRelease = r.GenerateReleaseNote("1.2.3", "2099-09-10", new List<ChangeCatogory>() { ChangeCatogory.Obsoleted });

            var releases = Release.FromChangelog(File.ReadAllText("changelog1.md"));

            newRelease.MergeTo(releases[0], MergeMode.Group);

            var mergedChangelog = Release.ToChangeLog(releases);
            var baseline = File.ReadAllText("mergedChangelog1.md");
            Assert.AreEqual(baseline.Replace("\r\n", "\n").TrimEnd('\r', '\n'), mergedChangelog.Replace("\r\n", "\n"));
        }
    }

}
