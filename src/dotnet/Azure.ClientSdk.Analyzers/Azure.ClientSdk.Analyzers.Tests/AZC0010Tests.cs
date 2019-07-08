// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0010Tests
    {
        private readonly DiagnosticAnalyzerRunner _runner = new DiagnosticAnalyzerRunner(new ClientOptionsAnalyzer());

        [Fact]
        public async Task AZC0010ProducedForClientOptionsCtorWithNoServiceVersionDefault()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class SomeClientOptions { 

        public enum ServiceVersion
        {
            V2018_11_09 = 0
        }

        public ServiceVersion Version { get; }

        public SomeClientOptions(ServiceVersion /*MM*/version)
        {
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0010", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0010NotProducedForClientOptionsCtorWithServiceVersionDefault()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class SomeClientOptions { 

        public enum ServiceVersion
        {
            V2018_11_09 = 0
        }

        public ServiceVersion Version { get; }

        public SomeClientOptions(ServiceVersion version = ServiceVersion.V2018_11_09)
        {
            this.Version = version;
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            if (diagnostics == null)
            {
                return;
            }

            Assert.True(diagnostics.Where(d => d.Id == "AZC0010").FirstOrDefault() == null);
        }
    }
}
