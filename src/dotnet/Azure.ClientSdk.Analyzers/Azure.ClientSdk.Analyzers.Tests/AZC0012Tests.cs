﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Azure.ClientSdk.Analyzers.Tests 
{
    public class AZC0012Tests 
    {
        private readonly DiagnosticAnalyzerRunner _runner = new DiagnosticAnalyzerRunner(new AddConfigureAwaitAnalyzer());

        [Theory]
        [InlineData(LanguageVersion.CSharp7)]
        [InlineData(LanguageVersion.Latest)]
        public async Task AZC0012WarningOnTask(LanguageVersion version)
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class MyClass
    {
        public static async System.Threading.Tasks.Task Foo()
        {
            /*MM*/await System.Threading.Tasks.Task.Run(() => {});
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source, version);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0012", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Theory]
        [InlineData(LanguageVersion.CSharp7)]
        [InlineData(LanguageVersion.Latest)]
        public async Task AZC0012WarningOnTaskOfT(LanguageVersion version) 
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class MyClass
    {
        public static async System.Threading.Tasks.Task Foo()
        {
            var i = /*MM*/await System.Threading.Tasks.Task.Run(() => 42);
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source, version);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0012", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Theory]
        [InlineData(LanguageVersion.CSharp7)]
        [InlineData(LanguageVersion.Latest)]
        public async Task AZC0012WarningOnValueTask(LanguageVersion version) 
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class MyClass
    {
        private static int _x;

        public static async System.Threading.Tasks.ValueTask Foo()
        {
            /*MM*/await RunAsync();
        }

        private static async System.Threading.Tasks.ValueTask RunAsync()
        {
            _x++;
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source, version);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0012", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Theory]
        [InlineData(LanguageVersion.CSharp7)]
        [InlineData(LanguageVersion.Latest)]
        public async Task AZC0012WarningOnValueTaskOfT(LanguageVersion version) 
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class MyClass
    {
        public static async System.Threading.Tasks.ValueTask Foo()
        {
            var i = /*MM*/await GetValueAsync();
        }

        private static async System.Threading.Tasks.ValueTask<int> GetValueAsync()
        {
            return 0;
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source, version);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0012", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0012NoWarningOnExistingConfigureAwaitFalse()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class MyClass
    {
        public static async System.Threading.Tasks.Task Foo()
        {
           await System.Threading.Tasks.Task.Run(() => {}).ConfigureAwait(false);
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task AZC0012WarningOnTaskDelay()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class MyClass
    {
        public static async System.Threading.Tasks.Task Foo()
        {
            /*MM*/await System.Threading.Tasks.Task.Delay(42);
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0012", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0012NoWarningOnTaskYield()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class MyClass
    {
        public static async System.Threading.Tasks.Task Foo()
        {
           await System.Threading.Tasks.Task.Yield();
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task AZC0012NoWarningOnNested()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System.Threading.Tasks;

    public class MyClass
    {
        public static async Task CallFooAsync()
        {
            await FooAsync(await (getFoo()).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private static async Task FooAsync(bool foo) {}
        private static async Task<bool> getFoo() => true;
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task AZC0012WarningOnVariable()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class MyClass
    {
        public static async System.Threading.Tasks.Task Foo()
        {
            var task = System.Threading.Tasks.Task.Run(() => {});
            /*MM*/await task;
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0012", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0012WarningOnAsyncForeach()
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
            await foreach (var x in /*MM*/GetValuesAsync()) { }
        }

        private static async IAsyncEnumerable<int> GetValuesAsync() { yield break; }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0012", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0012NoWarningOnAsyncForeachExistingConfigureAwaitFalse()
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
            await foreach (var x in /*MM*/GetValuesAsync().ConfigureAwait(false)) { }
        }

        private static async IAsyncEnumerable<int> GetValuesAsync() { yield break; }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task AZC0012NoWarningOnForeach()
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
            foreach (var x in GetValuesAsync()) { }
        }

        private static IEnumerable<int> GetValuesAsync() { yield break; }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task AZC0012WarningOnAsyncEnumerableVariable()
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
            var enumerable = GetValuesAsync();
            await foreach (var x in /*MM*/enumerable) { }
        }

        private static async IAsyncEnumerable<int> GetValuesAsync() { yield break; }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0012", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0012WarningOnAsyncForeachOfCustomEnumerable()
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
            foreach (var y in new List<string>()) { }
            await foreach (var x in /*MM*/new AsyncEnumerable()) { }
        }

        private class AsyncEnumerable : IAsyncEnumerable<int> 
        {
            public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default) => null;
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0012", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0012WarningOnAsyncUsingVariable()
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
            await using(/*MM*/ad) { }
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

            Assert.Equal("AZC0012", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0012WarningOnAsyncUsing()
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
            await using(/*MM*/new AsyncDisposable()) { }
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

            Assert.Equal("AZC0012", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0012WarningOnAsyncUsingNoBraces()
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
            await using IAsyncDisposable x = /*MM0*/CreateAsyncDisposable(),
                                         y = /*MM1*/new AsyncDisposable();
        }

        private static IAsyncDisposable CreateAsyncDisposable() => new AsyncDisposable();

        private class AsyncDisposable : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => new ValueTask();
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            Assert.Equal(2, diagnostics.Length);

            Assert.Equal("AZC0012", diagnostics[0].Id);
            Assert.Equal("AZC0012", diagnostics[1].Id);
            AnalyzerAssert.DiagnosticLocation(testSource.MarkerLocations["MM0"], diagnostics[0].Location);
            AnalyzerAssert.DiagnosticLocation(testSource.MarkerLocations["MM1"], diagnostics[1].Location);
        }

        [Fact]
        public async Task AZC0012WarningOnAsyncUsingVariableNoBraces()
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
            await using var _ = /*MM*/ad;
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

            Assert.Equal("AZC0012", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0012NoWarningOnAsyncUsingExistingConfigureAwaitFalse()
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
            await using(ad.ConfigureAwait(false)) { }
        }
    
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

        [Fact]
        public async Task AZC0012NoWarningOnAsyncUsingNoBracesExistingConfigureAwaitFalse()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;

    public class MyClass
    {
        public static async Task Foo()
        {
            var ad = new AsyncDisposable();
            await using ConfiguredAsyncDisposable x = CreateAsyncDisposable().ConfigureAwait(false), y = ad.ConfigureAwait(false);
        }

        private static IAsyncDisposable CreateAsyncDisposable() => new AsyncDisposable();

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

        [Fact]
        public async Task AZC0012NoWarningOnUsing()
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
            using(new Disposable()) { }
        }
    
        private class Disposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task AZC0012NoWarningOnUsingNoBraces()
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
            using var _ = new Disposable();
        }
    
        private class Disposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task AZC0012NoWarningOnCSharp7() 
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class MyClass
    {
        public static async Task Foo()
        {
            foreach (var x in GetValuesAsync()) { }
            using(new Disposable()) { }
        }
    
        private class Disposable : IDisposable
        {
            public void Dispose() { }
        }

        private static IEnumerable<int> GetValuesAsync() { yield break; }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source, LanguageVersion.CSharp7);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task AZC0012NonCompilableCode() 
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class MyClass
    {
        public static async Task M00() => await Task.Foo();
        public static async Task M01() => await Task.Foo().ConfigureAwait(false);
        public static async Task M02() => await Task.Delay(0).ConfigureAwait(0);
        public static async Task M03() => await Task.Foo();
        public static async Task M10() { await foreach () { } }
        public static async Task M11() { await foreach (var x in ) { } }
        public static async Task M12() { await foreach (var x in enum.ConfigureAwait(false)) { } }
        public static async Task M20() { await using var ; }
        public static async Task M21() { await using var a = ; }
        public static async Task M22() { await using(){} }
        public static async Task M23() { await using(var ){} }
        public static async Task M24() { await using(var a = ){} }
    }
}
");
            await _runner.GetDiagnosticsAsync(testSource.Source);
        }
    }
}