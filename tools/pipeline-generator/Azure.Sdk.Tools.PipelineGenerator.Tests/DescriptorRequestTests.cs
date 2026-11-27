using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.Build.WebApi;
using Xunit;

namespace PipelineGenerator.Tests
{
    public class DescriptorRequestTests
    {
        [Theory]
        [InlineData("sdk/old/ci.yml", "old", null)]
        [InlineData("sdk/service/ci.preview.yml", "service", "preview")]
        [InlineData("sdk/service/ci.\u03b1.yml", "service", "\u03b1")]
        [InlineData("sdk/service/tests.weekly.yml", "service", "weekly")]
        public void ReconstructsPreviousIdentity(string path, string name, string variant)
        {
            var component = DescriptorRequest.ToComponent(path);
            Assert.Equal(name, component.Name);
            Assert.Equal(variant, component.Variant);
        }

        [Fact]
        public void RelocationPreservesIdAndUpdatesNameAndYaml()
        {
            var definition = CreateDefinition("sdk/old/ci.yml");
            Assert.True(DescriptorRequest.Relocate(definition, "Azure/azure-sdk-for-java",
                "java - new", "sdk/new/ci.preview.yml", "sdk/old/ci.yml"));
            Assert.Equal(123, definition.Id);
            Assert.Equal("java - new", definition.Name);
            Assert.Equal("sdk/new/ci.preview.yml", ((YamlProcess)definition.Process).YamlFilename);
        }

        [Fact]
        public void CompletedRelocationCanBeReplayed()
        {
            var definition = CreateDefinition("sdk/new/ci.yml");
            definition.Name = "java - new";
            Assert.False(DescriptorRequest.Relocate(definition, "Azure/azure-sdk-for-java",
                "java - new", "sdk/new/ci.yml", "sdk/old/ci.yml"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("sdk/different/ci.yml")]
        public void RejectsUnmappedOrAmbiguousRelocation(string previousPath)
        {
            var definition = CreateDefinition("sdk/old/ci.yml");
            Assert.Throws<InvalidOperationException>(() => DescriptorRequest.Relocate(definition,
                "Azure/azure-sdk-for-java", "java - new", "sdk/new/ci.yml", previousPath));
            Assert.Equal("sdk/old/ci.yml", ((YamlProcess)definition.Process).YamlFilename);
        }

        [Fact]
        public void RejectsAnotherRepository()
        {
            var definition = CreateDefinition("sdk/old/ci.yml");
            Assert.Throws<InvalidOperationException>(() => DescriptorRequest.Relocate(definition,
                "Azure/azure-sdk-for-python", "python - old", "sdk/old/ci.yml", null));
        }

        [Fact]
        public void ResolvesOldNameWithoutCreatingACompetingDefinition()
        {
            var definition = CreateDefinition("sdk/old/ci.yml");
            Assert.Same(definition, DescriptorRequest.ResolveDefinition(new[] { definition },
                "Azure/azure-sdk-for-java", "java - new", "java - old", "sdk/new/ci.yml", "sdk/old/ci.yml"));
        }

        [Fact]
        public void RejectsCompetingOldAndNewDefinitions()
        {
            var old = CreateDefinition("sdk/old/ci.yml");
            var competing = CreateDefinition("sdk/new/ci.yml");
            competing.Name = "java - new";
            competing.Id = 124;
            Assert.Throws<InvalidOperationException>(() => DescriptorRequest.ResolveDefinition(new[] { old, competing },
                "Azure/azure-sdk-for-java", "java - new", "java - old", "sdk/new/ci.yml", "sdk/old/ci.yml"));
        }

        [Fact]
        public void RejectsAnUnexpectedNameAlreadyOwningTheDescriptor()
        {
            var definition = CreateDefinition("sdk/new/ci.yml");
            definition.Name = "manually renamed";
            Assert.Throws<InvalidOperationException>(() => DescriptorRequest.ResolveDefinition(new[] { definition },
                "Azure/azure-sdk-for-java", "java - new", null, "sdk/new/ci.yml", null));
        }

        [Fact]
        public void ScannerAndRenameIdentityAgreeForSupportedVariants()
        {
            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(root, ".git"));
                var service = Directory.CreateDirectory(Path.Combine(root, "sdk", "service"));
                File.WriteAllText(Path.Combine(service.FullName, "ci.yml"), "");
                File.WriteAllText(Path.Combine(service.FullName, "ci.\u03b1.yml"), "");
                var scanner = new SdkComponentScanner(NullLogger<SdkComponentScanner>.Instance);
                var components = scanner.Scan(service, "ci.yml").ToArray();
                Assert.Equal(2, components.Length);
                foreach (var component in components)
                {
                    var previous = DescriptorRequest.ToComponent(component.RelativeYamlPath);
                    Assert.Equal(component.Name, previous.Name);
                    Assert.Equal(component.Variant, previous.Variant);
                }
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        private static BuildDefinition CreateDefinition(string yamlPath)
        {
            return new BuildDefinition
            {
                Id = 123,
                Name = "java - old",
                Repository = new BuildRepository { Name = "Azure/azure-sdk-for-java" },
                Process = new YamlProcess { YamlFilename = yamlPath }
            };
        }
    }
}
