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
        public void TestTypeSpecInputValidation()
        {
            string metadataFile = Path.GetTempFileName();
            try
            {
                SpecHelper.ValidateTypeSpecInput("tsp-location.yaml", metadataFile);
            }
            finally
            {
                File.Delete(metadataFile);
            }
        }

        [TestMethod]
        public void TestMissingTypeSpecLocationThrows()
        {
            string metadataFile = Path.GetTempFileName();
            try
            {
                Assert.ThrowsException<FileNotFoundException>(() =>
                    SpecHelper.ValidateTypeSpecInput("missing-tsp-location.yaml", metadataFile));
            }
            finally
            {
                File.Delete(metadataFile);
            }
        }

        [TestMethod]
        public void TestMissingMetadataThrows()
        {
            Assert.ThrowsException<FileNotFoundException>(() =>
                SpecHelper.ValidateTypeSpecInput("tsp-location.yaml", "missing-metadata.json"));
        }

        [TestMethod]
        public void TestGetApiVersions()
        {
            const string content = """
                {
                  "apiVersions": {
                    "Microsoft.ApiCenter": "2024-06-01-preview"
                  }
                }
                """;

            IReadOnlyDictionary<string, string> apiVersions = SpecHelper.GetApiVersions(content);

            Assert.AreEqual("2024-06-01-preview", apiVersions["Microsoft.ApiCenter"]);
        }

        [TestMethod]
        public void TestUnchangedApiVersionsProduceNoChange()
        {
            const string content = """
                { "apiVersions": { "Microsoft.ApiCenter": "2024-06-01-preview" } }
                """;

            Assert.IsNull(Program.CompareApiVersions(content, content));
        }

        [TestMethod]
        public void TestChangedApiVersionsProduceVersionChange()
        {
            const string current = """
                { "apiVersions": { "Microsoft.ApiCenter": "2024-06-01-preview" } }
                """;
            const string baseline = """
                { "apiVersions": { "Microsoft.ApiCenter": "2023-04-01" } }
                """;

            var change = Program.CompareApiVersions(current, baseline);

            Assert.IsNotNull(change);
            Assert.AreEqual("Microsoft.ApiCenter: 2023-04-01", change.OldValue);
            Assert.AreEqual("Microsoft.ApiCenter: 2024-06-01-preview", change.NewValue);
        }

    }
}
