using System.Threading.Tasks;
using Azure.ClientSdk.Analyzers.ModelName;
using Xunit;

using VerifyCS = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<
    Azure.ClientSdk.Analyzers.ModelName.GeneralSuffixAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests.ModelName
{
    public class SuffixAnalyzerBaseTests
    {
        [Fact]
        public async Task NonPublicClassIsNotChecked()
        {
            var test = @"using System.Text.Json;
namespace Azure.Test.Models;

class MonitorParameter
{
    public static MonitorParameter DeserializeMonitorParameter(JsonElement element)
    {
        return null;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
        [Fact]
        public async Task ClassWithoutSerliaizationMethodsIsNotChecked()
        {
            var test = @"namespace Azure.Test.Models;

public class MonitorParameter
{
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task ClassWithSerliazationMethodIsChecked()
        {
            var test = @"using System.Text.Json;
namespace Azure.NotModels;

public class MonitorParameter
{
    public static MonitorParameter DeserializeMonitorParameter(JsonElement element)
    {
        return null;
    }
}";
            var expected = VerifyCS.Diagnostic(GeneralSuffixAnalyzer.DiagnosticId).WithSpan(4, 14, 4, 30).WithArguments("MonitorParameter", "Parameter", "'MonitorContent' or 'MonitorPatch'");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task ClassWithDeserliazationMethodIsChecked()
        {
            var test = @"using System.Text.Json;
namespace Azure.NotModels;

// workaround since IUtf8JsonSerializable is internally shared
internal interface IUtf8JsonSerializable
{
    void Write(Utf8JsonWriter writer);
}
public class MonitorParameter : IUtf8JsonSerializable
{
    void IUtf8JsonSerializable.Write(Utf8JsonWriter writer)
    {
        return;
    }
}";
            var expected = VerifyCS.Diagnostic(GeneralSuffixAnalyzer.DiagnosticId).WithSpan(9, 14, 9, 30).WithArguments("MonitorParameter", "Parameter", "'MonitorContent' or 'MonitorPatch'");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task EnumIsNotChecked()
        {
            var test = @"namespace Azure.ResourceManager.Models;

public enum MonitorParameter
{
    One
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

    }
}
