// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.AddConfigureAwaitAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests 
{
    public class AZC0013Tests 
    {
        [Fact]
        public async Task AZC0013WarningOnExistingConfigureAwaitTrue()
        {
            const string code = @"
namespace RandomNamespace
{
    public class MyClass
    {
        public static async System.Threading.Tasks.Task Foo()
        {
            await System.Threading.Tasks.Task.Run(() => {}).[|ConfigureAwait(true)|];
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, "AZC0013");
        }

        [Fact]
        public async Task AZC0013WarningOnAsyncEnumerableExistingConfigureAwaitTrue()
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
            await foreach (var x in GetValuesAsync().[|ConfigureAwait(true)|]) { }
        }

        private static async IAsyncEnumerable<int> GetValuesAsync() { yield break; }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, "AZC0013");
        }

        [Fact]
        public async Task AZC0013WarningOnAsyncUsingExistingConfigureAwaitTrue()
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
            await using(ad.[|ConfigureAwait(true)|]) { }
        }
    
        private class AsyncDisposable : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => new ValueTask();
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, "AZC0013");
        }

        [Fact]
        public async Task AZC0013WarningOnAsyncUsingNoBracesExistingConfigureAwaitTrue()
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
            await using var x = ad.[|ConfigureAwait(true)|];
        }
    
        private class AsyncDisposable : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => new ValueTask();
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, "AZC0013");
        }

        [Fact]
        public async Task AZC0013DisabledNoWarningOnExistingConfigureAwaitTrue()
        {
            const string code = @"
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

#pragma warning disable AZC0013
            await foreach (var x in GetValuesAsync().ConfigureAwait(true)) { }
            await System.Threading.Tasks.Task.Run(() => {}).ConfigureAwait(true);
            await using var y = ad.ConfigureAwait(true);
            await using(ad.ConfigureAwait(true)) { }
#pragma warning restore AZC0013
        }
    
        private static async IAsyncEnumerable<int> GetValuesAsync() { yield break; }

        private class AsyncDisposable : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => new ValueTask();
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }
    }
}