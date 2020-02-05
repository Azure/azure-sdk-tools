// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;

namespace Azure.ClientSdk.Analyzers.Tests 
{
    public class AZC0103Tests 
    {
        private readonly DiagnosticAnalyzerRunner _runner = new DiagnosticAnalyzerRunner(new AsyncAnalyzer());

        private const string TaskExtensionsString = @"
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
        public async Task AZC0103WarningInAsyncMethod()
        {
            var testSource = TestSource.Read(@"
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
            FooImplAsync(true)./*MM*/EnsureCompleted();
        }

        private async Task FooImplAsync(bool async, CancellationToken ct = default(CancellationToken)) 
        {
            await Task.Yield();
        }
    }
}
" + TaskExtensionsString);

            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0103", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostic.Location);
        }

        [Fact]
        public async Task AZC0103WarningInAsyncLambda()
        {
            var testSource = TestSource.Read(@"
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
                FooImplAsync(true)./*MM*/EnsureCompleted();
            };
        }

        private async Task FooImplAsync(bool async, CancellationToken ct = default(CancellationToken)) 
        {
            await Task.Yield();
        }
    }
}
" + TaskExtensionsString);

            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0103", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostic.Location);
        }
        
        [Fact]
        public async Task AZC0103WarningInAsyncLambdaField()
        {
            var testSource = TestSource.Read(@"
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
            FooImplAsync(true)./*MM*/EnsureCompleted();
        };

        private static async Task FooImplAsync(bool async, CancellationToken ct = default(CancellationToken)) 
        {
            await Task.Yield();
        }
    }
}
" + TaskExtensionsString);

            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0103", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostic.Location);
        }
        
        [Fact]
        public async Task AZC0103NoWarningInAsyncMethodWithAsyncParameter()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core.Pipeline;
    public class MyClass
    {
        private async Task FooAsync(bool async, CancellationToken ct = default(CancellationToken))
        {
            await FooImplAsync(true, ct).ConfigureAwait(false);
        }

        private async Task FooImplAsync(bool async, CancellationToken ct = default(CancellationToken)) 
        {
            await Task.Yield();
        }
    }
}
" + TaskExtensionsString);

            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task AZC0103NoWarningInAsyncLambdaWithAsyncParameter()
        {
            var testSource = TestSource.Read(@"
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
                await FooImplAsync(true, ct).ConfigureAwait(false);
            };
        }

        private async Task FooImplAsync(bool async, CancellationToken ct = default(CancellationToken)) 
        {
            await Task.Yield();
        }
    }
}
" + TaskExtensionsString);

            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task AZC0103NoWarningInAsyncLambdaWithAsyncParameterInClosure()
        {
            var testSource = TestSource.Read(@"
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

            await f().ConfigureAwait(false);
        }

        private async Task FooImplAsync(bool async, CancellationToken ct = default(CancellationToken)) 
        {
            await Task.Yield();
        }
    }
}
" + TaskExtensionsString);

            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            Assert.Empty(diagnostics);
        }
    }
}