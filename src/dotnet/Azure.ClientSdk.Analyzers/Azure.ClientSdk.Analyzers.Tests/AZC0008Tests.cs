// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0008Tests
    {
        private readonly DiagnosticAnalyzerRunner _runner = new DiagnosticAnalyzerRunner(new ClientOptionsAnalyzer());

        [Fact]
        public async Task AZC0008ProducedForClientOptionsWithoutServiceVersionEnum()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class /*MM*/SomeClientOptions { 

    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0008", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0008NotProducedForClientOptionsWithServiceVersionEnum()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class SomeClientOptions { 

        public enum ServiceVersion
        {
            V2018_11_09 = 0
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            Assert.Empty(diagnostics.Where(d => d.Id == "AZC0008"));
        }
    }
}
