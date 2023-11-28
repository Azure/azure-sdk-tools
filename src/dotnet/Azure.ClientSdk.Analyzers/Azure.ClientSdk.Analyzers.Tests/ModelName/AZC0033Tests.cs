using System.Threading.Tasks;
using Azure.ClientSdk.Analyzers.ModelName;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

using VerifyCS = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<
    Azure.ClientSdk.Analyzers.ModelName.OperationSuffixAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests.ModelName
{
    public class AZC0033Tests
    {
        private const string diagnosticId = "AZC0033";

        [Fact]
        public async Task OperationClassIsNotChecked()
        {
            var test = @"using System.Text.Json;

using Azure;
using Azure.ResourceManager;
namespace Azure
{
    public class Operation
    {
    }
    public class Operation<T> 
    {
    }
}
namespace Azure.ResourceManager
{
    public class ArmOperation : Operation
    {
    }
    public class ArmOperation<T> : Operation<T>
    {
    }
}
namespace Azure.ResourceManager.Network.Models
{
    public class DnsOperation : Operation 
    {
    }
    public class DnsArmOperation : ArmOperation 
    {
    }
    public class DnsOperation<T> : Operation<T> 
    {
    }
    public class DnsArmOperation<T> : ArmOperation<T> 
    {
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task OperationSuffix()
        {
            var test = @"using System.Text.Json;
namespace Azure.ResourceManager.Network
{
    public class DnsOperation
    {
        public static DnsOperation DeserializeDnsOperation(JsonElement element)
        {
            return null;
        }
    }
    public class DnsArmOperation<T>
    {
        public static DnsArmOperation<T> DeserializeDnsArmOperation(JsonElement element)
        {
            return null;
        }
    }
}";
            DiagnosticResult[] expected = {
                VerifyCS.Diagnostic(diagnosticId).WithSpan(4, 18, 4, 30).WithArguments("DnsOperation", "Operation", "DnsData", "DnsInfo"),
                VerifyCS.Diagnostic(diagnosticId).WithSpan(11, 18, 11, 33).WithArguments("DnsArmOperation", "Operation", "DnsArmData", "DnsArmInfo")
            };
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}

