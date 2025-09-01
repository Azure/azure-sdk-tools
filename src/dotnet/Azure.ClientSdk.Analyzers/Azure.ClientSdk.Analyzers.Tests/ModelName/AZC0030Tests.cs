using System.Threading.Tasks;
using Xunit;

using VerifyCS = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<
    Azure.ClientSdk.Analyzers.ModelName.GeneralSuffixAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests.ModelName
{
    public class AZC0030Tests
    {
        private const string DiagnosticId = "AZC0030";

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
            var expectedMessage = $"We suggest renaming it to 'ResourceManagerResponseParametersContent' or 'ResourceManagerResponseParametersPatch' or another name with this suffix.";
            var expected = VerifyCS.Diagnostic(DiagnosticId).WithSpan(4, 18, 4, 36).WithArguments("ResponseParameters", "Parameters", expectedMessage);
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
            var expectedMessage = $"We suggest renaming it to 'ResourceManagerDiskOptionConfig' or another name with this suffix.";
            var expected = VerifyCS.Diagnostic(DiagnosticId).WithSpan(4, 18, 4, 28).WithArguments("DiskOption", "Option", expectedMessage);
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
            var expectedMessage = $"We suggest renaming it to 'SubTestDiskOptionConfig' or another name with this suffix.";
            var expected = VerifyCS.Diagnostic(DiagnosticId).WithSpan(6, 22, 6, 32).WithArguments("DiskOption", "Option", expectedMessage);
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
            var expectedMessage = $"We suggest renaming it to 'SubTestCreationResponsesResults' or another name with this suffix.";
            var expected = VerifyCS.Diagnostic(DiagnosticId).WithSpan(6, 22, 6, 39).WithArguments("CreationResponses", "Responses", expectedMessage);
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}
