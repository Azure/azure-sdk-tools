// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.AsyncAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0104Tests 
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
        public async Task AZC0104WarningConfigureAwaitInSyncScope()
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
            var awaitable = FooImplAsync(false).{|AZC0104:ConfigureAwait(false)|};
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
        public async Task AZC0104WarningEnsureCompletedOnField()
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
        public int Foo(Task<int> task)
        {
            return {|AZC0104:_task|}.EnsureCompleted();
        }

        private Task<int> _task = Task.FromResult(42);
    }
}";
            
            await Verifier.CreateAnalyzer(code)
                .WithSources(AzureCorePipelineTaskExtensions)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0104WarningEnsureCompletedOnVariable()
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
        public static async Task FooAsync()
        {
            Func<int, int> a = i => 
            {
                Task<int> task = Task.FromResult(0);
                task = FooImplAsync();
                return {|AZC0104:task|}.EnsureCompleted();
            };
        }

        private static async Task<int> FooImplAsync(CancellationToken ct = default(CancellationToken)) => 42;
    }
}";
            
            await Verifier.CreateAnalyzer(code)
                .WithSources(AzureCorePipelineTaskExtensions)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0104WarningEnsureCompletedOnParameter()
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
        public static int Foo(Task<int> task)
        {
            return {|AZC0104:task|}.EnsureCompleted();
        }

        private static async Task<int> FooImplAsync(CancellationToken ct = default(CancellationToken)) => 42;
    }
}";
            
            await Verifier.CreateAnalyzer(code)
                .WithSources(AzureCorePipelineTaskExtensions)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0104WarningEnsureCompletedOnProperty()
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
        public static int Foo(Task<int> task)
        {
            return {|AZC0104:FooImplAsync|}.EnsureCompleted();
        }

        private static Task<int> FooImplAsync => Task.FromResult(42);
    }
}";
            
            await Verifier.CreateAnalyzer(code)
                .WithSources(AzureCorePipelineTaskExtensions)
                .RunAsync();
        }
    }
}