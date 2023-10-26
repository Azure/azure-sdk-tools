using System.Threading.Tasks;
using Azure.ClientSdk.Analyzers.ModelName;
using Xunit;

using VerifyCS = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<
    Azure.ClientSdk.Analyzers.ModelName.DefinitionSuffixAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests.ModelName
{
    public class AZC0031Tests
    {
        private const string diagnosticId = "AZC0031";

        [Fact]
        public async Task ModelWithDefinitionSuffix()
        {
            var test = @"using System.Text.Json;
namespace Azure.ResourceManager.Network.Models
{
    public partial class AadAuthenticationDefinition
    {
    }
}";
            var expected = VerifyCS.Diagnostic(diagnosticId).WithSpan(4, 26, 4, 53).WithArguments("AadAuthenticationDefinition", "Definition");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task ArmResourceIsNotChecked()
        {
            var test = @"using System.Text.Json;
using Azure.ResourceManager;
namespace Azure.ResourceManager
{
    public class ArmResource {
    }
}
namespace Azure.ResourceManager.Network.Models
{
    public partial class AadAuthenticationDefinition: ArmResource
    {
        public static AadAuthenticationDefinition DeserializeAadAuthenticationDefinition(JsonElement element)
        {
            return null;
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task NotCheckIfRemovingSuffixIsAnotherType()
        {
            var test = @"using System.Text.Json;
using Azure.ResourceManager;
namespace Azure.ResourceManager.Network.Models
{
    public class AadAuthentication {
    }

    public partial class AadAuthenticationDefinition
    {
        public static AadAuthenticationDefinition DeserializeAadAuthenticationDefinition(JsonElement element)
        {
            return null;
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

    }
}

