// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.TypeNameAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0012Tests
    {
        [Fact]
        public async Task AZC0012ProducedForSingleWordTypeNames()
        {
            const string code = @"
namespace Azure.Data
{
    public class {|AZC0012:Program|} { }
}";

            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0012ProducedForSingleWordInterfaceNames()
        {
            const string code = @"
namespace Azure.Data
{
    public interface {|AZC0012:IProgram|} { }
}";

            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0012NotProducedForNonPublicTypes()
        {
            const string code = @"
namespace Azure.Data
{
    internal class Program { }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0012NotProducedForMultiWordTypes()
        {
            const string code = @"
namespace Azure.Data
{
    public class NiceProgram { }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0012NotProducedForNestedTypes()
        {
            const string code = @"
namespace Azure.Data
{
    public class NiceProgram {
        public class Wow { }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0012NotProducedForNestedInterfaces()
        {
            const string code = @"
namespace Azure.Data
{
    public class NiceProgram {
        public interface IWow { }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }
    }
}
