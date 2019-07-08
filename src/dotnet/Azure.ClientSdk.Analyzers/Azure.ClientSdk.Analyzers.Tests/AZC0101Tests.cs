// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0101Tests
    {
        private readonly DiagnosticAnalyzerRunner _runner = new DiagnosticAnalyzerRunner(new NewLineAnalyzer());

        [Fact]
        public async Task AZC00101ProducedForDoubleNewline()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{

/*MM*/
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0101", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC00101NotProducedForSingleEmptyLine()
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
        public async Task AZC00101ProducedForDoubleNewlineInsideIf()
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

            Assert.Equal("AZC0101", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC00101NotProducedForComments()
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
        public async Task AZC00101NotProducedForIfDefs()
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
        public async Task Azc00101ProducedForTripleNewline()
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

            Assert.Equal("AZC0101", diagnostics[0].Id);
            Assert.Equal("AZC0101", diagnostics[1].Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
            AnalyzerAssert.DiagnosticLocation(testSource.MarkerLocations["MM1"], diagnostics[1].Location);
        }
    }
}