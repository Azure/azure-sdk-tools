using System.Threading.Tasks;
using Xunit;

using VerifyCS = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<
    Azure.ClientSdk.Analyzers.ModelName.DataSuffixAnalyzer>;
using Azure.ClientSdk.Analyzers.ModelName;

namespace Azure.ClientSdk.Analyzers.Tests.ModelName
{
    public class AZC0032Tests
    {
        private const string diagnosticId = "AZC0032";

        [Fact]
        public async Task ModelClassWithDataSuffix()
        {
            var test = @"using System.Text.Json;
namespace Azure.ResourceManager.Network.Models
{
    public partial class AadAuthenticationData
    {
        public static AadAuthenticationData DeserializeAadAuthenticationData(JsonElement element)
        {
            return null;
        }
    }
}";
            var expected = VerifyCS.Diagnostic(diagnosticId).WithSpan(4, 26, 4, 47).WithArguments("AadAuthenticationData", "Data");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task ResourceDataClassesAreNotChecked()
        {
            var test = @"using System.Text.Json;
using Azure.ResourceManager.Models;
namespace Azure.ResourceManager.Models
{
    public class ResourceData {
    }
}
namespace Azure.ResourceManager.Models.Network
{
    public partial class AadAuthenticationData: ResourceData
    {
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task TrackedResourceDataClassesAreNotChecked()
        {
            var test = @"using System.Text.Json;
using Azure.ResourceManager.Models;
namespace Azure.ResourceManager.Models
{
    public class TrackedResourceData {
    }
}
namespace Azure.ResourceManager.Network.Models
{
    public partial class AadAuthenticationData: TrackedResourceData
    {
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}

