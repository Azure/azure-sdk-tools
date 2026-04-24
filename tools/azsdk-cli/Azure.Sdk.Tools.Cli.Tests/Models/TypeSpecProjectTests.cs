using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Tests.TypeSpecTests
{
    internal class TypeSpecProjectTests
    {
        [Test]
        public void Test_ParsePackageNamesFromMetadata_parses_yaml()
        {
            var metadataYaml = """
            languages:
              .NET:
                packageName: Azure.ResourceManager.Contoso
              Java:
                packageName: com.azure.resourcemanager.contoso
              Python:
                packageName: azure-mgmt-contoso
              JavaScript:
                packageName: "@azure/arm-contoso"
              Go:
                packageName: sdk/resourcemanager/contoso/armcontoso
            """;

            var packages = TypeSpecHelper.ParsePackageNamesFromMetadata(metadataYaml);

            Assert.IsNotNull(packages);
            Assert.That(packages.Count, Is.EqualTo(5));
            Assert.That(packages.Any(p => p.Language == SdkLanguage.DotNet && p.PackageName == "Azure.ResourceManager.Contoso"));
            Assert.That(packages.Any(p => p.Language == SdkLanguage.Java && p.PackageName == "com.azure.resourcemanager.contoso"));
            Assert.That(packages.Any(p => p.Language == SdkLanguage.Python && p.PackageName == "azure-mgmt-contoso"));
            Assert.That(packages.Any(p => p.Language == SdkLanguage.JavaScript && p.PackageName == "@azure/arm-contoso"));
            Assert.That(packages.Any(p => p.Language == SdkLanguage.Go && p.PackageName == "sdk/resourcemanager/contoso/armcontoso"));
        }
    }
}
