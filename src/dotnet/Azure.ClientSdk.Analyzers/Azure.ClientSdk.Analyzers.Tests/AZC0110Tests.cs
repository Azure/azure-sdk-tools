// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.AsyncAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests 
{
    public class AZC0110Tests 
    {
        [Fact]
        public async Task AZC0110WarningAwaitOnAsyncMethod() {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;

    public class MyClass
    {
        private static async Task FooAsync(bool async)
        {
            await {|AZC0110:Task.Delay(0)|}.ConfigureAwait(false);
        }
    }
}";

            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0110WarningAwaitOnVariable() {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;

    public class MyClass
    {
        private static async Task FooAsync(bool async)
        {
            var task = Task.Delay(0);
            await {|AZC0110:task|}.ConfigureAwait(false);
        }
    }
}";

            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0110WarningAwaitOnMethodWithVariableParameterValue() {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;

    public class MyClass
    {
        private static async Task FooAsync(bool async)
        {
            var b = false;
            await {|AZC0110:FooImplAsync(b)|}.ConfigureAwait(false);
        }

        private static async Task FooImplAsync(bool async) 
        {
            if (async) { await Task.Delay(0).ConfigureAwait(false); }
        }
    }
}";

            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0110WarningAwaitOnMethodWithMethodParameterValue() {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;

    public class MyClass
    {
        private static async Task FooAsync(bool async)
        {
            await {|AZC0110:FooImplAsync(B())|}.ConfigureAwait(false);
        }

        private static bool B() => false;

        private static async Task FooImplAsync(bool async) 
        {
            if (async) { await Task.Delay(0).ConfigureAwait(false); }
        }
    }
}";

            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0110WarningPublicAsyncMethodInSyncScopeInsideAwait() 
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;

    public class MyClass
    {
        private static async Task FooAsync(bool async)
        {
            await ({|AZC0110:async ? FooImplAsync(true) : FooImplAsync(false)|}).ConfigureAwait(false);
        }

        private static async Task<int> FooImplAsync(bool async)
            => async ? await Task.FromResult(42).ConfigureAwait(false) : 42;
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }
    
        [Fact]
        public async Task AZC0110NoWarningAsyncMethod()
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;

    public class MyClass
    {
        private static async Task FooAsync(bool async)
            => await FooImplAsync(async).ConfigureAwait(false);

        private static async Task<int> FooImplAsync(bool async)
            => async ? await Task.FromResult(42).ConfigureAwait(false) : 42;
    }
}";
            
            await Verifier.VerifyAnalyzerAsync(code);
        }
        
        [Fact]
        public async Task AZC0110NoWarningAsyncLocalFunction()
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;

    public class MyClass
    {
        internal static async Task FooAsync(bool async)
        {
            await FooImplAsync(async).ConfigureAwait(false);
            
            static async Task<int> FooImplAsync(bool async)
            {
                return async ? await Task.FromResult(42).ConfigureAwait(false) : 42;
            }
        }
    }
}";
            
            await Verifier.VerifyAnalyzerAsync(code);
        }
        
        [Fact]
        public async Task AZC0110NoWarningAwaitOnExtensionMethod()
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;

    public class MyClass
    {
        private static async Task FooAsync(bool async)
            => await FooImplAsync(async).Extend().ConfigureAwait(false);

        private static async Task<int> FooImplAsync(bool async)
            => async ? await Task.FromResult(42).ConfigureAwait(false) : 42;
    }

    public static class Ext
    {
        public static Task<int> Extend(this Task<int> t) => t;
    }
}";

            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0110NoWarningSyncMethod()
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;

    public class MyClass
    {
        private static Task<int> Foo(bool async) => FooImplAsync(async);

        private static async Task<int> FooImplAsync(bool async)
            => async ? await Task.FromResult(42).ConfigureAwait(false) : 42;
    }
}";
            
            await Verifier.VerifyAnalyzerAsync(code);
        }
    }
}