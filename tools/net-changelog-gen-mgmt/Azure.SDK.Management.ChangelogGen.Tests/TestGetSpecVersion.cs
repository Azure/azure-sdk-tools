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

        [TestMethod]
        public void TestGetAutoRestSpecConfigurationKind()
        {
            SpecConfigurationKind kind = SpecHelper.GetSpecConfigurationKind("autorest1.md", "missing-tsp-location.yaml");

            Assert.AreEqual(SpecConfigurationKind.AutoRest, kind);
        }

        [TestMethod]
        public void TestGetTypeSpecConfigurationKind()
        {
            string typeSpecLocationFile = Path.GetTempFileName();
            try
            {
                SpecConfigurationKind kind = SpecHelper.GetSpecConfigurationKind("missing-autorest.md", typeSpecLocationFile);

                Assert.AreEqual(SpecConfigurationKind.TypeSpec, kind);
            }
            finally
            {
                File.Delete(typeSpecLocationFile);
            }
        }

        [TestMethod]
        public void TestMissingSpecConfigurationThrows()
        {
            Assert.ThrowsException<FileNotFoundException>(() =>
                SpecHelper.GetSpecConfigurationKind("missing-autorest.md", "missing-tsp-location.yaml"));
        }

        [TestMethod]
        public void TestGetTypeSpecSource()
        {
            const string content = """
                directory: specification/contoso/Contoso.WidgetManager/
                commit: 431eb865a581da2cd7b9e953ae52cb146f31c2a6
                repo: Azure/azure-rest-api-specs
                """;

            TypeSpecSource source = SpecHelper.GetTypeSpecSource(content);

            Assert.AreEqual("Azure/azure-rest-api-specs/specification/contoso/Contoso.WidgetManager@431eb865a581da2cd7b9e953ae52cb146f31c2a6", source.Version);
            Assert.AreEqual("https://github.com/Azure/azure-rest-api-specs/tree/431eb865a581da2cd7b9e953ae52cb146f31c2a6/specification/contoso/Contoso.WidgetManager", source.Url);
        }

        [TestMethod]
        public void TestUnchangedTypeSpecSourceProducesNoChange()
        {
            const string content = "directory: specification/contoso\ncommit: abc123\nrepo: Azure/azure-rest-api-specs";

            Assert.IsNull(Program.CompareTypeSpecSource(content, content));
        }

        [TestMethod]
        public void TestChangedTypeSpecSourceProducesVersionChange()
        {
            const string current = "directory: specification/contoso\ncommit: def456\nrepo: Azure/azure-rest-api-specs";
            const string baseline = "directory: specification/contoso\ncommit: abc123\nrepo: Azure/azure-rest-api-specs";

            var change = Program.CompareTypeSpecSource(current, baseline);

            Assert.IsNotNull(change);
            Assert.AreEqual("Azure/azure-rest-api-specs/specification/contoso@abc123", change.OldValue);
            Assert.AreEqual("Azure/azure-rest-api-specs/specification/contoso@def456", change.NewValue);
            StringAssert.Contains(change.Description, "https://github.com/Azure/azure-rest-api-specs/tree/def456/specification/contoso");
        }
    }
}
