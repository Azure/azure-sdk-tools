// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;

namespace Azure.ClientSdk.Analyzers.Tests 
{
    public class AZC0105Tests 
    {
        private readonly DiagnosticAnalyzerRunner _runner = new DiagnosticAnalyzerRunner(new AsyncAnalyzer());

        [Fact]
        public async Task AZC0105WarningPublicAsyncMethodWithAsyncParameter()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    public class MyClass
    {
        public static async Task /*MM*/FooAsync(bool async, CancellationToken ct)
        {
            await Task.Yield();
        }
    }
}");

            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0105", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostic.Location);
        }

        [Fact]
        public async Task AZC0105WarningPublicSyncMethodWithAsyncParameter()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    public class MyClass
    {
        public static void /*MM*/Foo(bool async, CancellationToken ct) { }
    }
}");

            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0105", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostic.Location);
        }

        [Fact]
        public async Task AZC0105NoWarningInternalAsyncMethodWithAsyncParameter()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    internal class MyClass
    {
        public static async Task FooAsync(bool async, CancellationToken ct)
        {
            await Task.Yield();
        }
    }
}");

            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task AZC0105NoWarningPrivateSyncMethodWithAsyncParameter()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    public class MyClass
    {
        private void Foo(bool async, CancellationToken ct) { }
    }
}");

            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            Assert.Empty(diagnostics);
        }
    }
}