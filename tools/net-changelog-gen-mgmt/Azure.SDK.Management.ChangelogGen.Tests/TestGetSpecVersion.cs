using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.SDK.ChangelogGen.Utilities;

namespace Azure.SDK.ChangelogGen.Tests
{
    [TestClass]
    public class TestGetSpecVersion
    {
        [TestMethod]
        public void TestGetSpecVersionFromMd()
        {
            var content = File.ReadAllText("autorest1.md");
            List<string> tags = SpecHelper.GetSpecVersionTags(content, out string src);

            Assert.AreEqual("./specReadme.md", src);
            Assert.AreEqual(1, tags.Count);
            Assert.AreEqual("package-2021-02", tags[0]);
        }
    }
}
