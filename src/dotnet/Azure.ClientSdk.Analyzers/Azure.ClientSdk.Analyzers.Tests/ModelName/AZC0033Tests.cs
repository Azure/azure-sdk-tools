using System.Threading.Tasks;
using Azure.ClientSdk.Analyzers.ModelName;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

using VerifyCS = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<
    Azure.ClientSdk.Analyzers.ModelName.OperationSuffixAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Test.ModelName
{
    public class AZC0033Tests
    {
        [Fact]
        public async Task OperationClassIsNotChecked()
        {
            var test = @"
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
    internal class DnsOperation : Operation 
    {
    }
    internal class DnsArmOperation : ArmOperation 
    {
    }
    internal class DnsOperation<T> : Operation<T> 
    {
    }
    internal class DnsArmOperation<T> : ArmOperation<T> 
    {
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task OperationSuffix()
        {
            var test = @"
namespace Azure.ResourceManager.Network.Models
{
    public class DnsOperation
    {
    }
    public class DnsArmOperation<T>
    {
    }
}";
            DiagnosticResult[] expected = {
                VerifyCS.Diagnostic(OperationSuffixAnalyzer.DiagnosticId).WithSpan(4, 18, 4, 30).WithArguments("DnsOperation", "Operation", "DnsData", "DnsInfo"),
                VerifyCS.Diagnostic(OperationSuffixAnalyzer.DiagnosticId).WithSpan(7, 18, 7, 33).WithArguments("DnsArmOperation", "Operation", "DnsArmData", "DnsArmInfo")
            };
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}

