using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

using VerifyCS = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<
    Azure.ClientSdk.Analyzers.ModelName.OptionsSuffixAnalyzer>;

// Test cases for `Options` suffix. We allow `Options` suffix for property bags.
// So need to skip property bags which do not have any serialization codes.
namespace Azure.ClientSdk.Analyzers.Tests.ModelName
{
    public class AZC0036Tests
    {
        private const string DiagnosticId = "AZC0036";

        // This test validates that the analyzer is triggered when the model is in the Azure.ResourceManager namespace
        // and has serialization methods.
        [Theory]
        [InlineData("Azure.ResourceManager")]
        [InlineData("Azure.ResourceManager.Models")]
        [InlineData("Azure.ResourceManager.SomeOtherNs")]
        public async Task WithAzureResourceManagerNamespaceAndSerialization(string ns)
        {
            var test = @"using System.Text.Json;
namespace " + ns + @"
{
    internal interface IUtf8JsonSerializable
    {
        void Write(Utf8JsonWriter writer);
    };

    public class DiskOptions: IUtf8JsonSerializable
    {
        void IUtf8JsonSerializable.Write(Utf8JsonWriter writer) {}
    }
}";
            var expectedMessage = GetDiagnosticMessage("DiskOptions", ns);
            var expected = VerifyCS.Diagnostic(DiagnosticId).WithSpan(9, 18, 9, 29).WithArguments("DiskOptions", "Options", expectedMessage);
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }


        // This test validates that the analyzer is triggered when the model is in the Azure.ResourceManager namespace
        // and has deserialization methods.
        [Theory]
        [InlineData("Azure.ResourceManager")]
        [InlineData("Azure.ResourceManager.Models")]
        [InlineData("Azure.ResourceManager.SomeOtherNs")]
        public async Task WithAzureResourceManagerNamespaceAndDeserializationMethod(string ns)
        {
            var test = @"using System.Text.Json;
namespace " + ns + @"
{
    public class ResponseOptions
    {
        public static ResponseOptions DeserializeResponseOptions(JsonElement element)
        {
            return null;
        }
    }
}";
            var expectedMessage = GetDiagnosticMessage("ResponseOptions", ns);
            var expected = VerifyCS.Diagnostic(DiagnosticId).WithSpan(4, 18, 4, 33).WithArguments("ResponseOptions", "Options", expectedMessage);
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Theory]
        [InlineData("Azure.ResourceManager")]
        [InlineData("Azure.ResourceManager.Models")]
        [InlineData("Azure.ResourceManager.SomeOtherNs")]
        public async Task WithAzureResourceManagerNamespaceRoundTripModel(string ns)
        {
            var test = @"using System.Text.Json;
namespace " + ns + @"
{
    internal interface IUtf8JsonSerializable
    {
        void Write(Utf8JsonWriter writer);
    };


    public class ResponseOptions : IUtf8JsonSerializable
    {
        void IUtf8JsonSerializable.Write(Utf8JsonWriter writer) {}
        public static ResponseOptions DeserializeResponseOptions(JsonElement element)
        {
            return null;
        }
    }
}";
            var expectedMessage = GetDiagnosticMessage("ResponseOptions", ns);
            var expected = VerifyCS.Diagnostic(DiagnosticId).WithSpan(10, 18, 10, 33).WithArguments("ResponseOptions", "Options", expectedMessage);
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Theory]
        [InlineData("Azure.ResourceManager")]
        [InlineData("Azure.ResourceManager.Models")]
        [InlineData("Azure.ResourceManager.SomeOtherNs")]
        public async Task WithNestedNamespaceAndSerialization(string ns)
        {
            var test = @"using System.Text.Json;
namespace " + ns + @"
{
    namespace SubTest
    {
         internal interface IUtf8JsonSerializable
        {
            void Write(Utf8JsonWriter writer);
        };

        public class DiskOptions: IUtf8JsonSerializable
        {
            void IUtf8JsonSerializable.Write(Utf8JsonWriter writer) {}
        }
    }
}";
            var expectedMessage = GetDiagnosticMessage("DiskOptions", ns + ".SubTest");
            var expected = VerifyCS.Diagnostic(DiagnosticId).WithSpan(11, 22, 11, 33).WithArguments("DiskOptions", "Options", expectedMessage);
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        // This test validates that the analyzer is not triggered when the model is in the Azure.ResourceManager namespace
        // and has no serialization methods.
        [Theory]
        [InlineData("Azure.ResourceManager")]
        [InlineData("Azure.ResourceManager.Models")]
        [InlineData("Azure.ResourceManager.SomeOtherNs")]
        public async Task WithAzureResourceManagerNamespaceAndNoSerialization(string ns)
        {
            var test = @"using System.Text.Json;
namespace " + ns + @"
{
    public class DiskOptions
    {
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Theory]
        [InlineData("Azure.ResourceManager")]
        [InlineData("Azure.ResourceManager.Models")]
        [InlineData("Azure.ResourceManager.SomeOtherNs")]
        public async Task WithAzureResourceManagerNamespaceAndOtherMethods(string ns)
        {
            var test = @"using System.Text.Json;
namespace " + ns + @"
{
    public class DiskOptions
    {
        public void SomeMethod() {}
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // This test validates that the analyzer does not trigger on a class that is not in the Azure.ResourceManager namespace
        // despite it having serialization.
        [Theory]
        [InlineData("Azure.Foo.Models")]
        [InlineData("Azure.Foo")]
        public async Task NonManagementModelWithSerializationMethod(string ns)
        {
            var test = @"using System.Text.Json;
namespace " + ns + @"
{
    internal interface IUtf8JsonSerializable
    {
        void Write(Utf8JsonWriter writer);
    };

    public class DiskOptions: IUtf8JsonSerializable
    {
        void IUtf8JsonSerializable.Write(Utf8JsonWriter writer) {}
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Theory]
        [InlineData("Azure.Foo.Models")]
        [InlineData("Azure.Foo")]
        public async Task NonManagementModelWithDeserializationMethod(string ns)
        {
            var test = @"using System.Text.Json;
namespace " + ns + @"
{
    public class ResponseOptions
    {
        public static ResponseOptions DeserializeResponseOptions(JsonElement element)
        {
            return null;
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Theory]
        [InlineData("Azure.Foo.Models")]
        [InlineData("Azure.Foo")]
        public async Task NonManagementRoundTripModel(string ns)
        {
            var test = @"using System.Text.Json;
namespace " + ns + @"
{
    internal interface IUtf8JsonSerializable
    {
        void Write(Utf8JsonWriter writer);
    };


    public class ResponseOptions : IUtf8JsonSerializable
    {
        void IUtf8JsonSerializable.Write(Utf8JsonWriter writer) {}
        public static ResponseOptions DeserializeResponseOptions(JsonElement element)
        {
            return null;
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Theory]
        [InlineData("Azure.Foo.Models")]
        [InlineData("Azure.Foo")]
        public async Task NonManagementModelWithNoMethods(string ns)
        {
            var test = @"using System.Text.Json;
namespace " + ns + @"
{
    public class DiskOptions
    {
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        private static string GetDiagnosticMessage(string typeName, string namespaceName)
        {
            string namespacePrefix = GetNamespacePrefix(namespaceName);

            // Generate suggestions based on the namespace prefix like the actual analyzer does
            if (!string.IsNullOrEmpty(namespacePrefix))
            {
                // The actual analyzer does NOT remove the "Options" suffix - it keeps the full type name
                string suggestionText = $"'{namespacePrefix}{typeName}Settings' or '{namespacePrefix}{typeName}Config'";
                
                return $"The `Options` suffix is reserved for input models described by https://azure.github.io/azure-sdk/dotnet_introduction.html#dotnet-parameters. " +
                    $"Please rename `{typeName}` to {suggestionText} or another suitable name according to our guidelines at https://azure.github.io/azure-sdk/general_design.html#model-types for output or roundtrip models.";
            }
            
            // For non-ResourceManager namespaces, return generic message (though this shouldn't occur in our tests)
            return $"The `Options` suffix is reserved for input models described by https://azure.github.io/azure-sdk/dotnet_introduction.html#dotnet-parameters. " +
                $"Please rename `{typeName}` according to our guidelines at https://azure.github.io/azure-sdk/general_design.html#model-types for output or roundtrip models.";
        }

        private static string GetNamespacePrefix(string namespaceName)
        {
            // Match the logic from NamingSuggestionHelper.GetNamespacePrefix()
            var namespaceParts = namespaceName.Split('.');
            
            // Skip generic parts that don't provide context and find the most specific meaningful part
            for (int i = namespaceParts.Length - 1; i >= 0; i--)
            {
                var part = namespaceParts[i];
                
                // Skip generic parts that don't provide context
                if (part.Equals("Models", System.StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("Internal", System.StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("Common", System.StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("Azure", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                
                // Convert to PascalCase and return the most specific part
                return ConvertToPascalCase(part);
            }
            
            // If we can't find a good namespace part, try to extract from the full namespace
            // For example: Azure.Messaging.EventHubs -> EventHubs
            if (namespaceParts.Length >= 3 && namespaceParts[0] == "Azure")
            {
                // Take the last meaningful part
                var lastPart = namespaceParts[namespaceParts.Length - 1];
                if (lastPart != "Models" && lastPart != "Internal" && lastPart != "Azure")
                {
                    return ConvertToPascalCase(lastPart);
                }
                
                // If last part is generic, try second to last
                if (namespaceParts.Length >= 4)
                {
                    return ConvertToPascalCase(namespaceParts[namespaceParts.Length - 2]);
                }
            }
            
            return ""; // For non-Azure namespaces or if we can't determine a good prefix
        }

        private static string ConvertToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";
                
            // Handle cases like "EventHubs" (already PascalCase)
            if (char.IsUpper(input[0]))
                return input;
                
            // Handle cases like "eventHubs" or "eventhubs"
            return char.ToUpper(input[0]) + input.Substring(1);
        }
    }
}
