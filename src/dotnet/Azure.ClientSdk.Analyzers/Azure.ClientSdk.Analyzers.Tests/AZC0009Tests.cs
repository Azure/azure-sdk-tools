// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0009Tests
    {
        private readonly DiagnosticAnalyzerRunner _runner = new DiagnosticAnalyzerRunner(new ClientOptionsAnalyzer());
               
        [Fact]
        public async Task AZC0009ProducedForClientOptionsWithOnlyDefaultCtor()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class /*MM*/SomeClientOptions {

        public enum ServiceVersion
        {
            V2018_11_09 = 0
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0009", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0009ProducedForClientOptionsWithoutServiceVersionInCtor()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class SomeClientOptions {

        public enum ServiceVersion
        {
            V2018_11_09 = 0
        }

        public /*MM*/SomeClientOptions()
        {
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0009", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0009ProducedForClientOptionsCtorWhereServiceVersionNotFirstParam()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class SomeClientOptions { 

        public enum ServiceVersion
        {
            V2018_11_09 = 0
        }

        public string AnOption { get; }

        public ServiceVersion Version { get; }

        public SomeClientOptions(string /*MM*/anOption, ServiceVersion version = ServiceVersion.V2018_11_09)
        {
            this.AnOption = anOption;
            this.Version = version;
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0009", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0009NotProducedForClientOptionsCtorWhereServiceVersionFirstParam()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class SomeClientOptions { 

        public enum ServiceVersion
        {
            V2018_11_09 = 0
        }

        public SomeClientOptions(ServiceVersion version = ServiceVersion.V2018_11_09)
        {
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            Assert.Empty(diagnostics);
        }
    }
}
