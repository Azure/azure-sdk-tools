// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0005Tests
    {
        private readonly DiagnosticAnalyzerRunner _runner = new DiagnosticAnalyzerRunner(new ClientConstructorAnalyzer());

        [Fact]
        public async Task AZC0005ProducedForClientTypesWithoutProtectedCtor()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class SomeClientOptions { }

    public class /*MM*/SomeClient
    {
        public SomeClient(string connectionString) {}
        public SomeClient(string connectionString, SomeClientOptions options) {}
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0005", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }
    }
}