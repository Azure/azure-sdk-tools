using System.Threading.Tasks;
using Xunit;

using VerifyCS = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<
    Azure.ClientSdk.Analyzers.ModelName.OptionsSuffixAnalyzer>;

// Test cases for `Options` suffix. We allow `Options` suffix for property bags.
// So need to skip property bags which do not have any serialization codes.
namespace Azure.ClientSdk.Analyzers.Tests.ModelName
{
    public class AZC0030OptionsSuffixTests
    {
        private const string DiagnosticId = "AZC0030";

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
            var expectedMessage = GetDiagnosticMessage("DiskOptions");
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
            var expectedMessage = GetDiagnosticMessage("ResponseOptions");
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
            var expectedMessage = GetDiagnosticMessage("ResponseOptions");
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
            var expectedMessage = GetDiagnosticMessage("DiskOptions");
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

        private static string GetDiagnosticMessage(string typeName)
        {
            return $"The `Options` suffix is reserved for input models described by https://azure.github.io/azure-sdk/dotnet_introduction.html#dotnet-parameters. " +
                $"Please rename `{typeName}` according to our guidelines at https://azure.github.io/azure-sdk/general_design.html#model-types for output or roundtrip models.";
        }
    }
}
