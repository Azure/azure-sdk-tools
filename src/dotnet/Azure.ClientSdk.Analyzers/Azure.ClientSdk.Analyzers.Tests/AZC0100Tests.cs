// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.AsyncAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests 
{
    public class AZC0100Tests 
    {
        [Theory]
        [InlineData(LanguageVersion.CSharp7)]
        [InlineData(LanguageVersion.Latest)]
        public async Task AZC0100WarningOnTask(LanguageVersion languageVersion)
        {
            const string code = @"
namespace RandomNamespace
{
    public class MyClass
    {
        public static async System.Threading.Tasks.Task Foo()
        {
            {|AZC0100:await System.Threading.Tasks.Task.Run(() => {})|];
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, languageVersion);
        }

        [Theory]
        [InlineData(LanguageVersion.CSharp7)]
        [InlineData(LanguageVersion.Latest)]
        public async Task AZC0100WarningOnTaskOfT(LanguageVersion languageVersion) 
        {
            const string code = @"
namespace RandomNamespace
{
    public class MyClass
    {
        public static async System.Threading.Tasks.Task Foo()
        {
            var i = {|AZC0100:await System.Threading.Tasks.Task.Run(() => 42)|};
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, languageVersion);
        }

        [Theory]
        [InlineData(LanguageVersion.CSharp7)]
        [InlineData(LanguageVersion.Latest)]
        public async Task AZC0100WarningOnValueTask(LanguageVersion languageVersion) 
        {
            const string code = @"
namespace RandomNamespace
{
    public class MyClass
    {
        private static int _x;

        public static async System.Threading.Tasks.ValueTask Foo()
        {
            {|AZC0100:await RunAsync()|};
        }

        private static async System.Threading.Tasks.ValueTask RunAsync()
        {
            _x++;
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, languageVersion);
        }

        [Theory]
        [InlineData(LanguageVersion.CSharp7)]
        [InlineData(LanguageVersion.Latest)]
        public async Task AZC0100WarningOnValueTaskOfT(LanguageVersion languageVersion) 
        {
            const string code = @"
namespace RandomNamespace
{
    public class MyClass
    {
        public static async System.Threading.Tasks.ValueTask Foo()
        {
            var i = {|AZC0100:await GetValueAsync()|};
        }

        private static async System.Threading.Tasks.ValueTask<int> GetValueAsync()
        {
            return 0;
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, languageVersion);
        }

        [Fact]
        public async Task AZC0100NoWarningOnExistingConfigureAwaitFalse()
        {
            const string code = @"
namespace RandomNamespace
{
    public class MyClass
    {
        public static async System.Threading.Tasks.Task Foo()
        {
           await System.Threading.Tasks.Task.Run(() => {}).ConfigureAwait(false);
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0100WarningOnTaskDelay()
        {
            const string code = @"
namespace RandomNamespace
{
    public class MyClass
    {
        public static async System.Threading.Tasks.Task Foo()
        {
            {|AZC0100:await System.Threading.Tasks.Task.Delay(42)|};
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0100NoWarningOnTaskYield()
        {
            const string code = @"
namespace RandomNamespace
{
    public class MyClass
    {
        public static async System.Threading.Tasks.Task Foo()
        {
           await System.Threading.Tasks.Task.Yield();
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0100NoWarningOnNested()
        {
            const string code = @"
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
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0100WarningOnVariable()
        {
            const string code = @"
namespace RandomNamespace
{
    public class MyClass
    {
        public static async System.Threading.Tasks.Task Foo()
        {
            var task = System.Threading.Tasks.Task.Run(() => {});
            {|AZC0100:await task|};
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0100WarningOnAsyncForeach()
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public class MyClass
    {
        public static async Task Foo()
        {
            await foreach (var x in {|AZC0100:GetValuesAsync()|}) { }
        }

        private static async IAsyncEnumerable<int> GetValuesAsync() { yield break; }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0100NoWarningOnAsyncForeachExistingConfigureAwaitFalse()
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public class MyClass
    {
        public static async Task Foo()
        {
            await foreach (var x in GetValuesAsync().ConfigureAwait(false)) { }
        }

        private static async IAsyncEnumerable<int> GetValuesAsync() { yield break; }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0100NoWarningOnForeach()
        {
            const string code = @"
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
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0100WarningOnAsyncEnumerableVariable()
        {
            const string code = @"
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
            await foreach (var x in {|AZC0100:enumerable|}) { }
        }

        private static async IAsyncEnumerable<int> GetValuesAsync() { yield break; }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0100WarningOnAsyncForeachOfCustomEnumerable()
        {
            const string code = @"
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
            await foreach (var x in {|AZC0100:new AsyncEnumerable()|}) { }
        }

        private class AsyncEnumerable : IAsyncEnumerable<int> 
        {
            public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default) => null;
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0100WarningOnAsyncUsingVariable()
        {
            const string code = @"
namespace RandomNamespace
{
    using System;
    using System.Threading.Tasks;

    public class MyClass
    {
        public static async Task Foo()
        {
            var ad = new AsyncDisposable();
            await using({|AZC0100:ad|}) { }
        }
    
        private class AsyncDisposable : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => new ValueTask();
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0100WarningOnAsyncUsing()
        {
            const string code = @"
namespace RandomNamespace
{
    using System;
    using System.Threading.Tasks;

    public class MyClass
    {
        public static async Task Foo()
        {
            await using({|AZC0100:new AsyncDisposable()|}) { }
        }
    
        private class AsyncDisposable : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => new ValueTask();
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0100WarningOnAsyncUsingNoBraces()
        {
            const string code = @"
namespace RandomNamespace
{
    using System;
    using System.Threading.Tasks;

    public class MyClass
    {
        public static async Task Foo()
        {
            await using IAsyncDisposable x = {|AZC0100:CreateAsyncDisposable()|},
                                         y = {|AZC0100:new AsyncDisposable()|};
        }

        private static IAsyncDisposable CreateAsyncDisposable() => new AsyncDisposable();

        private class AsyncDisposable : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => new ValueTask();
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0100WarningOnAsyncUsingVariableNoBraces()
        {
            const string code = @"
namespace RandomNamespace
{
    using System;
    using System.Threading.Tasks;

    public class MyClass
    {
        public static async Task Foo()
        {
            var ad = new AsyncDisposable();
            await using var _ = {|AZC0100:ad|};
        }
    
        private class AsyncDisposable : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => new ValueTask();
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0100NoWarningOnAsyncUsingExistingConfigureAwaitFalse()
        {
            const string code = @"
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
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0100NoWarningOnAsyncUsingNoBracesExistingConfigureAwaitFalse()
        {
            const string code = @"
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
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0100NoWarningOnUsing()
        {
            const string code = @"
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
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0100NoWarningOnUsingNoBraces()
        {
            const string code = @"
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
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0100NoWarningOnCSharp7() 
        {
            const string code = @"
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
}";
            await Verifier.VerifyAnalyzerAsync(code, languageVersion: LanguageVersion.CSharp7);
        }

        [Fact]
        public async Task AZC0100NonCompilableCode() 
        {
            const string code = @"
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
}";
            var analyzerTest = Verifier.CreateAnalyzer(code);
            analyzerTest.CompilerDiagnostics = CompilerDiagnostics.None;
            await analyzerTest.RunAsync();
        }
    }
}