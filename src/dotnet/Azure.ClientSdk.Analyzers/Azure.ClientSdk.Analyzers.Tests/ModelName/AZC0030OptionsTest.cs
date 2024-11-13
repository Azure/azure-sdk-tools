using System.Threading.Tasks;
using Xunit;

using VerifyCS = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<
    Azure.ClientSdk.Analyzers.ModelName.GeneralSuffixAnalyzer>;

// Test cases for `Options` suffix. We allow `Options` suffix for property bags.
// So need to skip property bags which do not have any serialization codes.
namespace Azure.ClientSdk.Analyzers.Tests.ModelName
{
    public class AZC0030OptionsSuffixTests
    {
        private const string diagnosticId = "AZC0030";

        [Theory]
        [InlineData("Azure.DataPlane.Models")]
        [InlineData("Azure.ResourceManager.Models")]
        public async Task WithoutAnySerialization(string ns)
        {
            var test = $@"using System.Text.Json;
namespace {ns};

public class DiskOptions
{{
}}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }


        [Theory]
        [InlineData("Azure.DataPlane.Models", false)]
        [InlineData("Azure.ResourceManager.Network.Models", true)]
        public async Task WithDeserializationMethod(string ns, bool hasError)
        {
            var test = $@"using System.Text.Json;
namespace {ns}
{{
    public class ResponseOptions
    {{
        public static ResponseOptions DeserializeResponseOptions(JsonElement element)
        {{
            return null;
        }}
    }}
}}";
            if (hasError)
            {
                var expected = VerifyCS.Diagnostic(diagnosticId).WithSpan(4, 18, 4, 33).WithArguments("ResponseOptions", "Options", "'ResponseConfig'");
                await VerifyCS.VerifyAnalyzerAsync(test, expected);
            }
            else
            {
                await VerifyCS.VerifyAnalyzerAsync(test);
            }
        }

        [Theory]
        [InlineData("Azure.DataPlane.Models", false)]
        [InlineData("Azure.ResourceManager.Batch.Models", true)]
        public async Task WithSerializationMethod(string ns, bool hasError)
        {
            var test = $@"using System.Text.Json;
namespace {ns}
{{
    internal interface IUtf8JsonSerializable
    {{
        void Write(Utf8JsonWriter writer);
    }};

    public class DiskOptions: IUtf8JsonSerializable
    {{
        void IUtf8JsonSerializable.Write(Utf8JsonWriter writer) {{}}
    }}
}}";
            if (hasError)
            {
                var expected = VerifyCS.Diagnostic(diagnosticId).WithSpan(9, 18, 9, 29).WithArguments("DiskOptions", "Options", "'DiskConfig'");
                await VerifyCS.VerifyAnalyzerAsync(test, expected);
            }
            else
            {
                await VerifyCS.VerifyAnalyzerAsync(test);
            }
        }
    }
}
