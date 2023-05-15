using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.SDK.ChangelogGen.Report;

namespace Azure.SDK.ChangelogGen.Tests
{
    [TestClass]
    public class TestGetReleaseVersion
    {
        [TestMethod]
        public void TestReleasesFromChangelogMdFile()
        {
            string content = File.ReadAllText("changelog1.md");
            List<Release> releases = Release.FromChangelog(content);
            Assert.AreEqual(7, releases.Count);

            Assert.AreEqual("1.1.0-beta.1", releases[0].Version);
            Assert.AreEqual("Unreleased", releases[0].ReleaseDate);

            Assert.AreEqual("1.0.1", releases[1].Version);
            Assert.AreEqual("2023-02-20", releases[1].ReleaseDate);
        }
    }
}
