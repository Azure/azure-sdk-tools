// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;

namespace Azure.ClientSdk.Analyzers.Tests 
{
    public class AZC0101Tests 
    {
        private readonly DiagnosticAnalyzerRunner _runner = new DiagnosticAnalyzerRunner(new AsyncAnalyzer());

        [Fact]
        public async Task AZC0101WarningOnExistingConfigureAwaitTrue()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class MyClass
    {
        public static async System.Threading.Tasks.Task Foo()
        {
            await System.Threading.Tasks.Task.Run(() => {})./*MM*/ConfigureAwait(true);
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0101", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0101WarningOnAsyncEnumerableExistingConfigureAwaitTrue()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public class MyClass
    {
        public static async Task Foo()
        {
            await foreach (var x in GetValuesAsync()./*MM*/ConfigureAwait(true)) { }
        }

        private static async IAsyncEnumerable<int> GetValuesAsync() { yield break; }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0101", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0101WarningOnAsyncUsingExistingConfigureAwaitTrue()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System;
    using System.Threading.Tasks;

    public class MyClass
    {
        public static async Task Foo()
        {
            var ad = new AsyncDisposable();
            await using(ad./*MM*/ConfigureAwait(true)) { }
        }
    
        private class AsyncDisposable : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => new ValueTask();
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0101", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0101WarningOnAsyncUsingNoBracesExistingConfigureAwaitTrue()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System;
    using System.Threading.Tasks;

    public class MyClass
    {
        public static async Task Foo()
        {
            var ad = new AsyncDisposable();
            await using var x = ad./*MM*/ConfigureAwait(true);
        }
    
        private class AsyncDisposable : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => new ValueTask();
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0101", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0101DisabledNoWarningOnExistingConfigureAwaitTrue()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class MyClass
    {
        public static async System.Threading.Tasks.Task Foo()
        {
            var ad = new AsyncDisposable();

#pragma warning disable AZC0101
            await foreach (var x in GetValuesAsync().ConfigureAwait(true)) { }
            await System.Threading.Tasks.Task.Run(() => {}).ConfigureAwait(true);
            await using var y = ad.ConfigureAwait(true);
            await using(ad.ConfigureAwait(true)) { }
#pragma warning restore AZC0101
        }
    
        private static async IAsyncEnumerable<int> GetValuesAsync() { yield break; }

        private class AsyncDisposable : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => new ValueTask();
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            Assert.Empty(diagnostics);
        }
    }
}