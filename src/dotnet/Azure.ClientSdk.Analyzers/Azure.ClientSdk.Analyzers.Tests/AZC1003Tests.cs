// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC1003Tests
    {
        private readonly DiagnosticAnalyzerRunner _runner = new DiagnosticAnalyzerRunner(new FileTypeNameAnalyzer());

        [Fact]
        public async Task AZC1003ProducedForMissnamedFiles()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class /*MM*/SomeClient
    {
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);


            Assert.Equal("AZC1003", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC1003NotProducedForNonPublicTypes()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    class SomeClient
    {
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task AZC1003NotProducedForCorrectlyNamedFiles()
        {
            var testSource = TestSource.Read(
                $@"
namespace RandomNamespace
{{
    public class {DiagnosticProject.DefaultFilePathPrefix}<T>
    {{
    }}
}}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task AZC1003NotProducedForNestedTypes()
        {
            var testSource = TestSource.Read(
                $@"
namespace RandomNamespace
{{
    public class {DiagnosticProject.DefaultFilePathPrefix}
    {{
        public class Nested {{}}
    }}
}}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            Assert.Empty(diagnostics);
        }
    }
}