using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.SDK.ChangelogGen.Tests
{
    [TestClass]
    public class TestGetSpecVersion
    {
        [TestMethod]
        public void TestGetSpecVersionFromMd()
        {
            var content = File.ReadAllText("autorest1.md");
            string version = Program.GetSpecVersion(content);

            Assert.AreEqual("2021-02-01", version);
        }
    }
}
