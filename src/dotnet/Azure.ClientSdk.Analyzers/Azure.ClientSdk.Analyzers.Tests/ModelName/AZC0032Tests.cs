using System.Threading.Tasks;
using Xunit;

using VerifyCS = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<
    Azure.ClientSdk.Analyzers.ModelName.DataSuffixAnalyzer>;
using Azure.ClientSdk.Analyzers.ModelName;

namespace Azure.ClientSdk.Analyzers.Tests.ModelName
{
    public class AZC0032Tests
    {
        [Fact]
        public async Task ClassUnderNonModelsNamespaceIsNotChecked()
        {
            var test = @"
namespace Azure.ResourceManager.Network.Temp
{
    public partial class AadAuthenticationData
    {
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task ModelClassWithDataSuffix()
        {
            var test = @"
namespace Azure.ResourceManager.Network.Models
{
    public partial class AadAuthenticationData
    {
    }
}";
            var expected = VerifyCS.Diagnostic(DataSuffixAnalyzer.DiagnosticId).WithSpan(4, 26, 4, 47).WithArguments("AadAuthenticationData", "Data");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task ResourceDataClassesAreNotChecked()
        {
            var test = @"
using Azure.ResourceManager.Models;
namespace Azure.ResourceManager.Models
{
    public class ResourceData {
    }
}
namespace Azure.ResourceManager.Network.Models
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
            var test = @"
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

