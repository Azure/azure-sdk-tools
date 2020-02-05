// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;

namespace Azure.ClientSdk.Analyzers.Tests 
{
    public class AZC0106Tests 
    {
        private readonly DiagnosticAnalyzerRunner _runner = new DiagnosticAnalyzerRunner(new AsyncAnalyzer());

        private const string TaskExtensionsString = @"
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
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core.Pipeline;
    public class MyClass
    {
        public static void Foo()
        {
            /*MM*/FooImplAsync().EnsureCompleted();
        }

        private static async Task<int> FooImplAsync(CancellationToken ct = default(CancellationToken)) 
        {
            await Task.Yield();
            return 42;
        }
    }
}
" + TaskExtensionsString);

            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0106", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostic.Location);
        }

        [Fact]
        public async Task AZC0106WarningOnAsyncMethodCallInLambda()
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
        public async Task Foo()
        {
            Func<int, int> a = i => /*MM*/FooImplAsync().EnsureCompleted();
            await Task.Yield();
        }

        private async Task<int> FooImplAsync(CancellationToken ct = default(CancellationToken)) 
        {
            await Task.Yield();
            return 42;
        }
    }
}
" + TaskExtensionsString);

            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0106", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostic.Location);
        }

        [Fact]
        public async Task AZC0106WarningOnAsyncMethodCallInDelegate()
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
        public async Task Foo()
        {
            Action a = delegate
            {
                /*MM*/FooImplAsync().EnsureCompleted();
            };
            await Task.Yield();
        }

        private async Task<int> FooImplAsync(CancellationToken ct = default(CancellationToken)) 
        {
            await Task.Yield();
            return 42;
        }
    }
}
" + TaskExtensionsString);

            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0106", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostic.Location);
        }

        [Fact]
        public async Task AZC0106WarningOnAsyncMethodCallInLocalFunction()
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
        public async Task Foo()
        {
            await Task.Yield();

            void FooImpl()
            {
                /*MM*/FooImplAsync().EnsureCompleted();
            }
        }

        private async Task<int> FooImplAsync(CancellationToken ct = default(CancellationToken)) 
        {
            await Task.Yield();
            return 42;
        }
    }
}
" + TaskExtensionsString);

            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0106", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostic.Location);
        }

        [Fact]
        public async Task AZC0106NoWarningOnAsyncMethodCallWithAsyncParameter()
        {
            var testSource = TestSource.Read(@"
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
        {
            await Task.Yield();
            return 42;
        }
    }
}
" + TaskExtensionsString);

            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            Assert.Empty(diagnostics);
        }

    }
}