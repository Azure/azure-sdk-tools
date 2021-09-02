// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.AsyncAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0106Tests 
    {
        private const string AzureCorePipelineTaskExtensions = @"
namespace Azure.Core.Pipeline
{
    using System.Threading.Tasks;

    internal static class TaskExtensions
    {
#pragma warning disable AZC0102
        public static T EnsureCompleted<T>(this Task<T> task) => task.GetAwaiter().GetResult();
#pragma warning restore AZC0102
    }
}
";

        [Fact]
        public async Task AZC0106WarningOnAsyncMethodCall()
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core.Pipeline;
    public class MyClass
    {
        public static void Foo()
        {
            {|AZC0106:FooImplAsync()|}.EnsureCompleted();
        }

        private static async Task<int> FooImplAsync(CancellationToken ct = default(CancellationToken)) 
        {
            await Task.Yield();
            return 42;
        }
    }
}";
            
            await Verifier.CreateAnalyzer(code)
                .WithSources(AzureCorePipelineTaskExtensions)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0106WarningOnAsyncMethodCallInLambda()
        {
            const string code = @"
namespace RandomNamespace
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core.Pipeline;
    public class MyClass
    {
        public async Task Foo()
        {
            Func<int, int> a = i => {|AZC0106:FooImplAsync()|}.EnsureCompleted();
            await Task.Yield();
        }

        private async Task<int> FooImplAsync(CancellationToken ct = default(CancellationToken)) 
        {
            await Task.Yield();
            return 42;
        }
    }
}";
            
            await Verifier.CreateAnalyzer(code)
                .WithSources(AzureCorePipelineTaskExtensions)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0106WarningOnAsyncMethodCallInDelegate()
        {
            const string code = @"
namespace RandomNamespace
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core.Pipeline;
    public class MyClass
    {
        public async Task Foo()
        {
            Action a = delegate
            {
                {|AZC0106:FooImplAsync()|}.EnsureCompleted();
            };
            await Task.Yield();
        }

        private async Task<int> FooImplAsync(CancellationToken ct = default(CancellationToken)) 
        {
            await Task.Yield();
            return 42;
        }
    }
}";
            
            await Verifier.CreateAnalyzer(code)
                .WithSources(AzureCorePipelineTaskExtensions)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0106WarningOnAsyncMethodCallInLocalFunction()
        {
            const string code = @"
namespace RandomNamespace
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core.Pipeline;
    public class MyClass
    {
        public async Task Foo()
        {
            await Task.Yield();

            void FooImpl()
            {
                {|AZC0106:FooImplAsync()|}.EnsureCompleted();
            }
        }

        private async Task<int> FooImplAsync(CancellationToken ct = default(CancellationToken)) 
        {
            await Task.Yield();
            return 42;
        }
    }
}";
            
            await Verifier.CreateAnalyzer(code)
                .WithSources(AzureCorePipelineTaskExtensions)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0106NoWarningOnAsyncMethodCallWithAsyncParameter()
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core.Pipeline;
    public class MyClass
    {
        public static void Foo()
        {
            FooImplAsync(false).EnsureCompleted();
        }

        private static async Task<int> FooImplAsync(bool async, CancellationToken ct = default(CancellationToken))
            => async ? await Task.FromResult(42).ConfigureAwait(false) : 42;
    }
}";
            
            await Verifier.CreateAnalyzer(code)
                .WithSources(AzureCorePipelineTaskExtensions)
                .RunAsync();
        }

    }
}