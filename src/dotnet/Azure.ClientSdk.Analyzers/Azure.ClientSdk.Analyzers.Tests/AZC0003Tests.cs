// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0003Tests
    {
        private readonly DiagnosticAnalyzerRunner _runner = new DiagnosticAnalyzerRunner(new ClientMethodsAnalyzer());

        [Fact]
        public async Task AZC0002ProducedForNonVirtualMethods()
        {
            var testSource = TestSource.Read(@"
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public Task /*MM0*/GetAsync(CancellationToken cancellationToken = default)
        {
            return null;
        }

        public void /*MM1*/Get(CancellationToken cancellationToken = default)
        {
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            Assert.Equal(2, diagnostics.Length);

            Assert.Equal("AZC0003", diagnostics[0].Id);
            Assert.Equal("AZC0003", diagnostics[1].Id);
            AnalyzerAssert.DiagnosticLocation(testSource.MarkerLocations["MM0"], diagnostics[0].Location);
            AnalyzerAssert.DiagnosticLocation(testSource.MarkerLocations["MM1"], diagnostics[1].Location);
        }

        [Fact]
        public async Task AZC0002SkippedForPrivateMethods()
        {
            var testSource = TestSource.Read(@"
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        private Task GetAsync(CancellationToken cancellationToken = default)
        {
            return null;
        }

        private void Get(CancellationToken cancellationToken = default)
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