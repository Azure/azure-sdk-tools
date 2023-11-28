using System.Threading.Tasks;
using Azure.ClientSdk.Analyzers.ModelName;
using Xunit;

using VerifyCS = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<
    Azure.ClientSdk.Analyzers.ModelName.GeneralSuffixAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests.ModelName
{
    public class AZC0030Tests
    {
        private const string diagnosticId = "AZC0030";

        [Fact]
        public async Task GoodSuffix()
        {
            var test = @"using System.Text.Json;
namespace Azure.ResourceManager.Models;

public class MonitorContent
{
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task ParametersSuffix()
        {
            var test = @"using System.Text.Json;
namespace Azure.ResourceManager
{
    public class ResponseParameters
    {
        public static ResponseParameters DeserializeResponseParameters(JsonElement element)
        {
            return null;
        }
    }
}";
            var expected = VerifyCS.Diagnostic(diagnosticId).WithSpan(4, 18, 4, 36).WithArguments("ResponseParameters", "Parameters", "'ResponseContent' or 'ResponsePatch'");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task RequestSuffix()
        {
            var test = @"using System.Text.Json;
namespace Azure.ResourceManager.Models
{
    public class DiskOption
    {
        public static DiskOption DeserializeDiskOption(JsonElement element)
        {
            return null;
        }
    }
}";
            var expected = VerifyCS.Diagnostic(diagnosticId).WithSpan(4, 18, 4, 28).WithArguments("DiskOption", "Option", "'DiskConfig'");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task OptionSuffixWithNestedNameSpace()
        {
            var test = @"using System.Text.Json;
namespace Azure.ResourceManager.Models
{
    namespace SubTest
    {
        public class DiskOption
        {
        }
    }
}";
            var expected = VerifyCS.Diagnostic(diagnosticId).WithSpan(6, 22, 6, 32).WithArguments("DiskOption", "Option", "'DiskConfig'");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task ResponsesSuffix()
        {
            var test = @"using System.Text.Json;
namespace Azure.ResourceManager.Models
{
    namespace SubTest
    {
        public class CreationResponses
        {
            public static CreationResponses DeserializeCreationResponses(JsonElement element)
            {
                return null;
            }
        }
    }
}";
            var expected = VerifyCS.Diagnostic(diagnosticId).WithSpan(6, 22, 6, 39).WithArguments("CreationResponses", "Responses", "'CreationResults'");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}
