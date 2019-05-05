// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0004Tests
    {
        private readonly DiagnosticAnalyzerRunner _runner = new DiagnosticAnalyzerRunner(new ClientMethodsAnalyzer());

        [Fact]
        public async Task AZC0004ProducedForMethodsWithoutSyncAlternative()
        {
            var testSource = TestSource.Read(@"
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task /*MM0*/GetAsync(CancellationToken cancellationToken = default)
        {
            return null;
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0004", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.MarkerLocations["MM0"], diagnostics[0].Location);
        }
    }
}