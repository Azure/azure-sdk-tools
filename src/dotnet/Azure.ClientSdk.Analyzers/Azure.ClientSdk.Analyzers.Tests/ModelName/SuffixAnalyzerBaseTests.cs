using System.Threading.Tasks;
using Xunit;

using VerifyCS = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<
    Azure.ClientSdk.Analyzers.ModelName.GeneralSuffixAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests.ModelName
{
    public class SuffixAnalyzerBaseTests
    {
        private const string DiagnosticId = "AZC0030";

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

        [Theory]
        [InlineData(@"namespace Azure.Test.Models;

public class MonitorParameter
{
}")]
        [InlineData(@"namespace Azure.Models.Test;

public class MonitorParameter
{
}")]
        public async Task ClassWithoutSerializationMethodsButInModelsNamespaceIsChecked(string test)
        {
            var expectedMessage = $"We suggest renaming it to 'TestMonitorParameterContent' or 'TestMonitorParameterPatch' or another name with this suffix.";
            var expected = VerifyCS.Diagnostic(DiagnosticId).WithSpan(3, 14, 3, 30).WithArguments("MonitorParameter", "Parameter", expectedMessage);
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
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
            var expectedMessage = $"We suggest renaming it to 'NotModelsMonitorParameterContent' or 'NotModelsMonitorParameterPatch' or another name with this suffix.";
            var expected = VerifyCS.Diagnostic(DiagnosticId).WithSpan(4, 14, 4, 30).WithArguments("MonitorParameter", "Parameter", expectedMessage);
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task ClassWithDeserializationMethodIsChecked()
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
            var expectedMessage = $"We suggest renaming it to 'NotModelsMonitorParameterContent' or 'NotModelsMonitorParameterPatch' or another name with this suffix.";
            var expected = VerifyCS.Diagnostic(DiagnosticId).WithSpan(9, 14, 9, 30).WithArguments("MonitorParameter", "Parameter", expectedMessage);
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
