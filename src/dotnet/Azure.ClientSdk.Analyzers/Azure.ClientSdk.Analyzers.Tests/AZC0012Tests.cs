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
        public async Task AZC0001ProducedForSingleWordTypeNames()
        {
            const string code = @"
namespace Azure.Data
{
    public class [|Program|] { }
}";

            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0001NotProducedForNonPublicTypes()
        {
            const string code = @"
namespace Azure.Data
{
    internal class Program { }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0001NotProducedForMultiWordTypes()
        {
            const string code = @"
namespace Azure.Data
{
    public class NiceProgram { }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }
    }
}