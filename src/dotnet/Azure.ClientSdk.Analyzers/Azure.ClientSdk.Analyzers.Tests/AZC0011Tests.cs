// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Threading.Tasks;
using Xunit;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0011Tests
    {
        private readonly DiagnosticAnalyzerRunner _runner = new DiagnosticAnalyzerRunner(new ClientAssemblyAttributesAnalyzer());

        [Fact]
        public async Task AZC0011ProducedForNonTestIVTs()
        {
            var testSource = TestSource.Read(@"
[assembly:/*MM0*/System.Runtime.CompilerServices.InternalsVisibleTo(""Product, PublicKey=..."")]
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0011", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }
        
        [Fact]
        public async Task AZC0011NotProducedForTestAndMoqIVTs()
        {
            var testSource = TestSource.Read(@"
[assembly:System.Runtime.CompilerServices.InternalsVisibleTo(""Product.Test, PublicKey=..."")]
[assembly:System.Runtime.CompilerServices.InternalsVisibleTo(""Product.Tests, PublicKey=..."")]
[assembly:System.Runtime.CompilerServices.InternalsVisibleTo(""DynamicProxyGenAssembly2, PublicKey=..."")]
            ");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            Assert.Empty(diagnostics);
        }
    }
}
