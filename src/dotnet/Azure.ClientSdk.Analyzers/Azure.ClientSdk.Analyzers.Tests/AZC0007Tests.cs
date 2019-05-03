// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0007Tests
    {
        private readonly DiagnosticAnalyzerRunner _runner = new DiagnosticAnalyzerRunner(new ClientConstructorAnalyzer());

        [Fact]
        public async Task AZC0007ProducedForClientsWithoutOptionsCtor()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class SomeClient
    {
        public /*MM*/SomeClient() {}
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0007", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0006ProducedForClientsWithoutOptionsCtorWithArguments()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class SomeClient
    {
        protected SomeClient() {}
        public /*MM*/SomeClient(string connectionString) {}
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0007", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }
    }
}