// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.ClientAssemblyAttributesAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0011Tests
    {
        [Fact]
        public async Task AZC0011ProducedForNonTestIVTs()
        {
            const string code = @"
[assembly:{|AZC0011:System.Runtime.CompilerServices.InternalsVisibleTo(""Product, PublicKey=..."")|}]
";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0011NotProducedForTestAndMoqIVTs()
        {
            const string code = @"
[assembly:System.Runtime.CompilerServices.InternalsVisibleTo(""Product.Test, PublicKey=..."")]
[assembly:System.Runtime.CompilerServices.InternalsVisibleTo(""Product.Tests, PublicKey=..."")]
[assembly:System.Runtime.CompilerServices.InternalsVisibleTo(""DynamicProxyGenAssembly2, PublicKey=..."")]
";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0011NotProducedForBenchmarkIVTs()
        {
            const string code = @"
[assembly:System.Runtime.CompilerServices.InternalsVisibleTo(""Product.Benchmarks, PublicKey=..."")]
[assembly:System.Runtime.CompilerServices.InternalsVisibleTo(""Product.Performance, PublicKey=..."")]
[assembly:System.Runtime.CompilerServices.InternalsVisibleTo(""Product.Perf, PublicKey=..."")]
";
            await Verifier.VerifyAnalyzerAsync(code);
        }
    }
}
