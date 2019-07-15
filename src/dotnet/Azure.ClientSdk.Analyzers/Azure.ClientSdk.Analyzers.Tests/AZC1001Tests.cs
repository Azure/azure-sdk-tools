// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC1001Tests
    {
        private readonly DiagnosticAnalyzerRunner _runner = new DiagnosticAnalyzerRunner(new WhitespaceNewLineAnalyzer());

        [Fact]
        public async Task AZC1001ProducedForDoubleNewline()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{

/*MM*/
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC1001", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC1001NotProducedForSingleEmptyLine()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{

}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task AZC1001ProducedForDoubleNewlineInsideIf()
        {
            var testSource = TestSource.Read(@"
#define DEBUG

namespace RandomNamespace
{

#if DEBUG

class c {}

/*MM*/
#endif

}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC1001", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC1001NotProducedForComments()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{

//

}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task AZC1001NotProducedForIfDefs()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{

#if DEBUG

#endif

}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task AZC1001ProducedForTripleNewline()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{

/*MM*/
/*MM1*/
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            Assert.Equal(2, diagnostics.Length);

            Assert.Equal("AZC1001", diagnostics[0].Id);
            Assert.Equal("AZC1001", diagnostics[1].Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
            AnalyzerAssert.DiagnosticLocation(testSource.MarkerLocations["MM1"], diagnostics[1].Location);
        }
    }
}