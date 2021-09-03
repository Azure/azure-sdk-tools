// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.AsyncAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests 
{
    public class AZC0101Tests 
    {
        [Fact]
        public async Task AZC0101WarningOnExistingConfigureAwaitTrue()
        {
            const string code = @"
namespace RandomNamespace
{
    public class MyClass
    {
        public static async System.Threading.Tasks.Task Foo()
        {
            await System.Threading.Tasks.Task.Run(() => {}).{|AZC0101:ConfigureAwait(true)|};
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0101WarningOnAsyncEnumerableExistingConfigureAwaitTrue()
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
            await foreach (var x in GetValuesAsync().{|AZC0101:ConfigureAwait(true)|}) { }
        }

        private static async IAsyncEnumerable<int> GetValuesAsync() { yield break; }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0101WarningOnAsyncUsingExistingConfigureAwaitTrue()
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
            await using(ad.{|AZC0101:ConfigureAwait(true)|}) { }
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
        public async Task AZC0101WarningOnAsyncUsingNoBracesExistingConfigureAwaitTrue()
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
            await using var x = ad.{|AZC0101:ConfigureAwait(true)|};
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
        public async Task AZC0101DisabledNoWarningOnExistingConfigureAwaitTrue()
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
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }
    }
}