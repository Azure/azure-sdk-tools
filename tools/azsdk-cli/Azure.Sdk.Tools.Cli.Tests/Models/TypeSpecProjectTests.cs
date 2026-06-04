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

        [Test]
        public void Test_ParsePackageNamesFromMetadata_parses_yamlCollection()
        {
            var metadataYaml = """
                emitterVersion: 0.2.0
                generatedAt: 2026-05-28T23:48:21.437Z
                typespec:
                  namespace: Microsoft.ContainerService
                  documentation: Azure Kubernetes Fleet Manager api client.
                  type: management
                languages:
                  unknown:
                    - emitterName: "@azure-tools/typespec-autorest"
                      outputDir: c:/git/azure-rest-api-specs/specification/containerservice/resource-manager/Microsoft.ContainerService/fleet
                      serviceDir: sdk/containerservicefleet
                  python:
                    - emitterName: "@azure-tools/typespec-python"
                      packageName: azure-mgmt-containerservicefleet
                      namespace: azure.mgmt.containerservicefleet
                      outputDir: "{output-dir}/sdk/containerservice/azure-mgmt-containerservicefleet"
                      flavor: azure
                      serviceDir: sdk/containerservice
                  csharp:
                    - emitterName: "@azure-typespec/http-client-csharp-mgmt"
                      packageName: Azure.ResourceManager.ContainerServiceFleet
                      namespace: Azure.ResourceManager.ContainerServiceFleet
                      outputDir: "{output-dir}/sdk/fleet/Azure.ResourceManager.ContainerServiceFleet"
                      serviceDir: sdk/containerservicefleet
                  java:
                    - emitterName: "@azure-tools/typespec-java"
                      packageName: com.azure.resourcemanager:azure-resourcemanager-containerservicefleet
                      namespace: com.azure.resourcemanager.containerservicefleet
                      outputDir: "{output-dir}/sdk/containerservicefleet/azure-resourcemanager-containerservicefleet"
                      flavor: azure
                      serviceDir: sdk/containerservicefleet
                  go:
                    - emitterName: "@azure-tools/typespec-go"
                      packageName: sdk/resourcemanager/containerservicefleet/armcontainerservicefleet/v3
                      namespace: sdk/resourcemanager/containerservicefleet/armcontainerservicefleet/v3
                      outputDir: "{output-dir}/sdk/resourcemanager/containerservicefleet/armcontainerservicefleet"
                      flavor: azure
                      serviceDir: sdk/resourcemanager/containerservicefleet
                  typescript:
                    - emitterName: "@azure-tools/typespec-ts"
                      packageName: "@azure/arm-containerservicefleet"
                      namespace: "@azure/arm-containerservicefleet"
                      outputDir: "{output-dir}/sdk/containerservice/arm-containerservicefleet"
                      flavor: azure
                      serviceDir: sdk/containerservice
                sourceConfigPath: c:/git/azure-rest-api-specs/specification/containerservice/resource-manager/Microsoft.ContainerService/fleet/tspconfig.yaml            
            """;

            var packages = TypeSpecHelper.ParsePackageNamesFromMetadata(metadataYaml);

            Assert.IsNotNull(packages);
            Assert.That(packages.Count, Is.EqualTo(5));
            Assert.That(packages.Any(p => p.Language == SdkLanguage.DotNet && p.PackageName == "Azure.ResourceManager.ContainerServiceFleet"));
            Assert.That(packages.Any(p => p.Language == SdkLanguage.Java && p.PackageName == "azure-resourcemanager-containerservicefleet" && p.Group == "com.azure.resourcemanager"));
            Assert.That(packages.Any(p => p.Language == SdkLanguage.Python && p.PackageName == "azure-mgmt-containerservicefleet"));
            Assert.That(packages.Any(p => p.Language == SdkLanguage.JavaScript && p.PackageName == "@azure/arm-containerservicefleet"));
            Assert.That(packages.Any(p => p.Language == SdkLanguage.Go && p.PackageName == "sdk/resourcemanager/containerservicefleet/armcontainerservicefleet/v3"));
        }
    }
}
