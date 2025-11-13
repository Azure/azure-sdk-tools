using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class NamingSuggestionHelperTests
    {
        [Theory]
        [InlineData("MyType", "Data", null, "'TestMyTypeData'")]
        [InlineData("MyType", "Data", "Info", "'TestMyTypeData' or 'TestMyTypeInfo'")]
        [InlineData("SomeClass", "Settings", "Config", "'TestSomeClassSettings' or 'TestSomeClassConfig'")]
        public void GetNamespacedSuggestion_WithVariousSuffixes_ReturnsExpectedFormat(string typeName, string primarySuffix, string secondarySuffix, string expected)
        {
            // Arrange
            var typeSymbol = CreateTestTypeSymbol(typeName, "Azure.Test");

            // Act
            var result = NamingSuggestionHelper.GetNamespacedSuggestion(typeName, typeSymbol, primarySuffix, secondarySuffix);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Operation`1", "Data", null, "'TestOperationData'")]
        [InlineData("IClient", "Service", null, "'TestClientService'")]
        [InlineData("IOperation`2", "Data", "Info", "'TestOperationData' or 'TestOperationInfo'")]
        public void GetNamespacedSuggestion_WithCleanedTypeNames_ReturnsExpectedFormat(string typeName, string primarySuffix, string secondarySuffix, string expected)
        {
            // Arrange
            var typeSymbol = CreateTestTypeSymbol(typeName, "Azure.Test");

            // Act
            var result = NamingSuggestionHelper.GetNamespacedSuggestion(typeName, typeSymbol, primarySuffix, secondarySuffix);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Client", "'TestClient' or 'TestServiceClient'")]
        [InlineData("Service", "'TestService' or 'TestManager'")]
        [InlineData("Response", "'TestResponse' or 'TestResult'")]
        [InlineData("Operation", "'TestOperation' or 'TestProcess'")]
        [InlineData("Data", "'TestData' or 'TestInfo'")]
        [InlineData("Options", "'TestOptions' or 'TestSettings'")]
        [InlineData("Settings", "'TestSettings' or 'TestConfig'")]
        [InlineData("Config", "'TestConfig' or 'TestSettings'")]
        [InlineData("Builder", "'TestBuilder' or 'TestFactory'")]
        [InlineData("Manager", "'TestManager' or 'TestService'")]
        [InlineData("Provider", "'TestProvider' or 'TestFactory'")]
        [InlineData("Handler", "'TestHandler' or 'TestProcessor'")]
        [InlineData("Helper", "'TestHelper' or 'TestUtil'")]
        [InlineData("Util", "'TestUtil' or 'TestHelper'")]
        [InlineData("Utils", "'TestUtil' or 'TestHelper'")]
        [InlineData("Factory", "'TestFactory' or 'TestBuilder'")]
        [InlineData("Info", "'TestInfo' or 'TestData'")]
        [InlineData("Result", "'TestResult' or 'TestResponse'")]
        [InlineData("Type", "'TestType' or 'TestKind'")]
        [InlineData("Kind", "'TestKind' or 'TestType'")]
        [InlineData("State", "'TestState' or 'TestStatus'")]
        [InlineData("Status", "'TestStatus' or 'TestState'")]
        public void GetCommonTypeSuggestion_WithCommonTypeNames_ReturnsExpectedSuggestions(string typeName, string expected)
        {
            // Arrange
            var typeSymbol = CreateTestTypeSymbol(typeName, "Azure.Test");

            // Act
            var result = NamingSuggestionHelper.GetCommonTypeSuggestion(typeName, typeSymbol);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("BlobClient", "'TestBlobClient' or 'TestServiceClient'")]
        [InlineData("TableClient", "'TestTableClient' or 'TestServiceClient'")]
        [InlineData("QueueClient", "'TestQueueClient' or 'TestServiceClient'")]
        public void GetCommonTypeSuggestion_WithCompositeClientNames_ReturnsExpectedSuggestions(string typeName, string expected)
        {
            // Arrange
            var typeSymbol = CreateTestTypeSymbol(typeName, "Azure.Test");

            // Act
            var result = NamingSuggestionHelper.GetCommonTypeSuggestion(typeName, typeSymbol);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("UnknownType", "'TestUnknownTypeClient' or 'TestUnknownTypeService'")]
        [InlineData("CustomClass", "'TestCustomClassClient' or 'TestCustomClassService'")]
        public void GetCommonTypeSuggestion_WithUnknownTypeNames_ReturnsDefaultSuggestions(string typeName, string expected)
        {
            // Arrange
            var typeSymbol = CreateTestTypeSymbol(typeName, "Azure.Test");

            // Act
            var result = NamingSuggestionHelper.GetCommonTypeSuggestion(typeName, typeSymbol);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Azure.Storage.Blobs", "Blobs")]
        [InlineData("Azure.Messaging.EventHubs", "EventHubs")]
        [InlineData("Azure.ResourceManager.Compute", "Compute")]
        [InlineData("Azure.ResourceManager.Models", "ResourceManager")]
        [InlineData("Azure.Test.Models", "Test")]
        [InlineData("Azure.Models.Test", "Test")]
        [InlineData("Azure.Common.Internal", "Custom")]
        [InlineData("Custom.Namespace", "Namespace")]
        [InlineData("CompanyName.ProductName", "ProductName")]
        public void GetNamespacePrefix_WithVariousNamespaces_ReturnsExpectedPrefix(string namespaceName, string expected)
        {
            // Arrange
            var typeSymbol = CreateTestTypeSymbol("TestType", namespaceName);

            // Act
            var result = NamingSuggestionHelper.GetNamespacePrefix(typeSymbol);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void GetNamespacePrefix_WithGlobalNamespace_ReturnsCustomFallback()
        {
            // Arrange
            var typeSymbol = CreateTestTypeSymbolInGlobalNamespace("TestType");

            // Act
            var result = NamingSuggestionHelper.GetNamespacePrefix(typeSymbol);

            // Assert
            Assert.Equal("Custom", result);
        }

        [Theory]
        [InlineData("Operation`1", "Operation")]
        [InlineData("Operation`2", "Operation")]
        [InlineData("IClient", "Client")]
        [InlineData("IAsyncEnumerable", "AsyncEnumerable")]
        [InlineData("IOperation`1", "Operation")]
        [InlineData("RegularClass", "RegularClass")]
        [InlineData("I", "I")] // Edge case - single character
        [InlineData("Ix", "Ix")] // Edge case - two characters starting with I
        public void CleanTypeName_WithVariousInputs_ReturnsCleanedName(string input, string expected)
        {
            // Arrange & Act
            var result = NamingSuggestionHelper.CleanTypeName(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("BlobClient", true)]
        [InlineData("TableClient", true)]
        [InlineData("QueueClient", true)]
        [InlineData("Client", false)]
        [InlineData("ServiceClient", true)]
        [InlineData("client", false)] // lowercase
        [InlineData("", false)]
        [InlineData("NotAClient", true)]
        public void IsCompositeClientName_WithVariousInputs_ReturnsExpectedResult(string input, bool expected)
        {
            // Arrange & Act
            var result = NamingSuggestionHelper.IsCompositeClientName(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("", "Custom")]
        [InlineData("test", "Test")]
        [InlineData("Test", "Test")]
        [InlineData("testName", "TestName")]
        [InlineData("TestName", "TestName")]
        [InlineData("eventHubs", "EventHubs")]
        [InlineData("ALLCAPS", "ALLCAPS")]
        [InlineData("a", "A")]
        public void ConvertToPascalCase_WithVariousInputs_ReturnsExpectedResult(string input, string expected)
        {
            // Arrange & Act
            var result = NamingSuggestionHelper.ConvertToPascalCase(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Azure.Storage.Blobs", "Blobs")]
        [InlineData("Azure.Messaging.EventHubs.Models", "EventHubs")]
        [InlineData("Azure.ResourceManager.Storage.Models", "Storage")]
        [InlineData("Azure.Test.Internal", "Test")]
        [InlineData("Azure.Common", "Custom")]
        [InlineData("Azure", "Custom")]
        [InlineData("Azure.Models", "Custom")]
        public void GetNamespacePrefix_WithAzureNamespaces_SkipsGenericParts(string namespaceName, string expected)
        {
            // Arrange
            var typeSymbol = CreateTestTypeSymbol("TestType", namespaceName);

            // Act
            var result = NamingSuggestionHelper.GetNamespacePrefix(typeSymbol);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("lowerCase", "LowerCase")]
        [InlineData("snake_case", "Snake_case")]
        [InlineData("kebab-case", "Kebab-case")]
        [InlineData("MixedCASE", "MixedCASE")]
        [InlineData("123number", "123number")]
        public void ConvertToPascalCase_WithEdgeCases_HandlesCorrectly(string input, string expected)
        {
            // Arrange & Act
            var result = NamingSuggestionHelper.ConvertToPascalCase(input);

            // Assert
            Assert.Equal(expected, result);
        }

        private static INamedTypeSymbol CreateTestTypeSymbol(string typeName, string namespaceName)
        {
            var source = $@"
namespace {namespaceName}
{{
    public class {typeName.Replace("`", "")} {{ }}
}}";

            var compilation = CreateCompilation(source);
            var tree = compilation.SyntaxTrees.First();
            var semanticModel = compilation.GetSemanticModel(tree);

            var classDeclaration = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
            return semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
        }

        private static INamedTypeSymbol CreateTestTypeSymbolInGlobalNamespace(string typeName)
        {
            var source = $@"public class {typeName} {{ }}";

            var compilation = CreateCompilation(source);
            var tree = compilation.SyntaxTrees.First();
            var semanticModel = compilation.GetSemanticModel(tree);

            var classDeclaration = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
            return semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
        }

        private static CSharpCompilation CreateCompilation(string source)
        {
            return CSharpCompilation.Create(
                "TestAssembly",
                new[] { CSharpSyntaxTree.ParseText(source) },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }
    }
}
