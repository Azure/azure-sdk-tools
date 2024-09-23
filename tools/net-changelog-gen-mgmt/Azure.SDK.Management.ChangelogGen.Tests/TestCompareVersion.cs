using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.SDK.ChangelogGen.Compare;

namespace Azure.SDK.ChangelogGen.Tests
{
    [TestClass]
    public class TestCompareVersion
    {
        [TestMethod]
        public void TestVersionEqual()
        {
            StringValueChange? result = Program.CompareVersion("1.0.1", "1.0.1", "test version equal");
            Assert.IsNull(result);
        }

        [TestMethod]
        public void TestVersionNotEqual()
        {
            StringValueChange? result = Program.CompareVersion("1.0.2", "1.0.1", "test version equal");
            Assert.IsNotNull(result);
            Assert.AreEqual("1.0.2", result.NewValue);
            Assert.AreEqual("1.0.1", result.OldValue);
        }
    }
}
