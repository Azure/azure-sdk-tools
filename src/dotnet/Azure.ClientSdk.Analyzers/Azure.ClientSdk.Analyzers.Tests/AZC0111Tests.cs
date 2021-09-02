// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.AsyncAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests 
{
    public class AZC0111Tests 
    {
        private const string AzureCorePipelineTaskExtensions = @"
namespace Azure.Core.Pipeline
{
    using System.Threading.Tasks;

    internal static class TaskExtensions
    {
#pragma warning disable AZC0102
        public static void EnsureCompleted(this Task task) => task.GetAwaiter().GetResult();
#pragma warning restore AZC0102
    }
}
";

        [Fact]
        public async Task AZC0111WarningEnsureCompleted()
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core.Pipeline;

    public class MyClass
    {
        private static async Task FooAsync(bool async)
        {
            {|AZC0111:Task.Delay(0)|}.EnsureCompleted();
        }
    }
}";

            await Verifier.CreateAnalyzer(code)
                .WithSources(AzureCorePipelineTaskExtensions)
                .RunAsync();
        }
        
        [Fact]
        public async Task AZC0111WarningEnsureCompletedOnVariable()
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core.Pipeline;

    public class MyClass
    {
        private static async Task FooAsync(bool async)
        {
            var task = Task.Delay(0);
            {|AZC0111:task|}.EnsureCompleted();
        }
    }
}";

            await Verifier.CreateAnalyzer(code)
                .WithSources(AzureCorePipelineTaskExtensions)
                .RunAsync();
        }

        
    
        [Fact]
        public async Task AZC0111WarningEnsureCompletedWithAsyncParameter()
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core.Pipeline;

    public class MyClass
    {
        private static async Task FooAsync(bool async)
        {
            {|AZC0111:FooImplAsync(async)|}.EnsureCompleted();
        }

        private static async Task FooImplAsync(bool async) 
        {
            if (async) { await Task.Delay(0).ConfigureAwait(false); }
        }
    }
}";
            
            await Verifier.CreateAnalyzer(code)
                .WithSources(AzureCorePipelineTaskExtensions)
                .RunAsync();
        }
    }
}