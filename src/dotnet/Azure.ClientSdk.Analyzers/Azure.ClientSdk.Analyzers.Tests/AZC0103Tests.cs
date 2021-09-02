// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.AsyncAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0103Tests 
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
        public async Task AZC0103WarningInAsyncMethod()
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core.Pipeline;
    public class MyClass
    {
        public async Task Foo()
        {
            await Task.Yield();
            FooImplAsync(true).{|AZC0103:EnsureCompleted()|};
        }

        private static async Task<int> FooImplAsync(bool async, CancellationToken ct = default(CancellationToken))
            => async ? await Task.FromResult(42).ConfigureAwait(false) : 42;
    }
}";
            
            await Verifier.CreateAnalyzer(code)
                .WithSources(AzureCorePipelineTaskExtensions)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0103WarningInAsyncLambda()
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
        public void Foo()
        {
            Func<Task> f = async () => 
            {
                await Task.Yield();
                FooImplAsync(true).{|AZC0103:EnsureCompleted()|};
            };
        }

        private static async Task<int> FooImplAsync(bool async, CancellationToken ct = default(CancellationToken))
            => async ? await Task.FromResult(42).ConfigureAwait(false) : 42;
    }
}";
            
            await Verifier.CreateAnalyzer(code)
                .WithSources(AzureCorePipelineTaskExtensions)
                .RunAsync();
        }
        
        [Fact]
        public async Task AZC0103WarningInAsyncLambdaField()
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
        private Func<Task> _f = async () => 
        {
            await Task.Yield();
            FooImplAsync(true).{|AZC0103:EnsureCompleted()|};
        };

        private static async Task<int> FooImplAsync(bool async, CancellationToken ct = default(CancellationToken))
            => async ? await Task.FromResult(42).ConfigureAwait(false) : 42;
    }
}";
            
            await Verifier.CreateAnalyzer(code)
                .WithSources(AzureCorePipelineTaskExtensions)
                .RunAsync();
        }
        
        [Fact]
        public async Task AZC0103WarningInAsyncMethodOnGetAwaiter()
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core.Pipeline;
    public class MyClass
    {
        public async Task Foo()
        {
            await Task.Yield();
            FooImplAsync(true).{|AZC0103:GetAwaiter().GetResult()|};
        }

        private static async Task<int> FooImplAsync(bool async, CancellationToken ct = default(CancellationToken))
            => async ? await Task.FromResult(42).ConfigureAwait(false) : 42;
    }
}";
            
            await Verifier.CreateAnalyzer(code)
                .WithSources(AzureCorePipelineTaskExtensions)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0103NoWarningInAsyncMethodWithAsyncParameter()
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core.Pipeline;
    public class MyClass
    {
        private async Task FooAsync(bool async, CancellationToken ct = default(CancellationToken))
        {
            await FooImplAsync(async, ct).ConfigureAwait(false);
        }

        private static async Task<int> FooImplAsync(bool async, CancellationToken ct = default(CancellationToken))
            => async ? await Task.FromResult(42).ConfigureAwait(false) : 42;
    }
}";
            
            await Verifier.CreateAnalyzer(code)
                .WithSources(AzureCorePipelineTaskExtensions)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0103NoWarningInAsyncLambdaWithAsyncParameter()
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
        private void Foo(CancellationToken ct = default(CancellationToken))
        {
            Func<bool, Task> f = async (async) => 
            {
                await FooImplAsync(async, ct).ConfigureAwait(false);
            };
        }

        private static async Task<int> FooImplAsync(bool async, CancellationToken ct = default(CancellationToken))
            => async ? await Task.FromResult(42).ConfigureAwait(false) : 42;
    }
}";
            
            await Verifier.CreateAnalyzer(code)
                .WithSources(AzureCorePipelineTaskExtensions)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0103NoWarningInAsyncLambdaWithAsyncParameterInClosure()
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
        private async Task FooImpl(bool async, CancellationToken ct = default(CancellationToken))
        {
            Func<Task> f = async () => 
            {
                await Task.Yield();
                await FooImplAsync(true, ct).ConfigureAwait(false);
            };

            if (async)
            {
                await f().ConfigureAwait(false);
            }
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