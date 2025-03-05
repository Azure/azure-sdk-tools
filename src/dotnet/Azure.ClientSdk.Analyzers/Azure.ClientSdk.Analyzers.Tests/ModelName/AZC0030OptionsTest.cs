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

        [Fact]
        public async Task WithoutAnySerialization()
        {
            var test = @"using System.Text.Json;
namespace Azure.ResourceManager.Models;

public class DiskOptions
{
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task WithDeserializationMethod()
        {
            var test = @"using System.Text.Json;
namespace Azure.ResourceManager
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

        [Fact]
        public async Task WithSerializationMethod()
        {
            var test = @"using System.Text.Json;
namespace Azure.ResourceManager.Models
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

        [Fact]
        public async Task WithNestedNamespaceAndSerialization()
        {
            var test = @"using System.Text.Json;
namespace Azure.ResourceManager.Models
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

        // This test validates that the analyzer is triggered when the model is directly in the Azure.ResourceManager namespace
        // and has serialization methods.
        [Fact]
        public async Task WithExactNamespaceAndSerialization()
        {
            var test = @"using System.Text.Json;
namespace Azure.ResourceManager
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

        // This test validates that the analyzer is not triggered when the model is directly in the Azure.ResourceManager namespace
        // and has no serialization methods.
        [Fact]
        public async Task WithExactNamespaceAndNoSerialization()
        {
            var test = @"using System.Text.Json;
namespace Azure.ResourceManager
{
    public class DiskOptions
    {
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // This test validates that the analyzer does not trigger on a class that is not in the Azure.ResourceManager.Models namespace
        // despite it having serialization.
        [Fact]
        public async Task NonManagementModelWithSerializationMethod()
        {
            var test = @"using System.Text.Json;
namespace Azure.Foo.Models
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

        [Fact]
        public async Task NonManagementModelWithDeserializationMethod()
        {
            var test = @"using System.Text.Json;
namespace Azure.Foo.Models
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

        [Fact]
        public async Task NonManagementModelWithNoMethods()
        {
            var test = @"using System.Text.Json;
namespace Azure.Foo.Models
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
