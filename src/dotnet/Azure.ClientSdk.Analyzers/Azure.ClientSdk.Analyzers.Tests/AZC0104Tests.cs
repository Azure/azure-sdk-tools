// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;

namespace Azure.ClientSdk.Analyzers.Tests 
{
    public class AZC0104Tests 
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
        public async Task AZC0104WarningEnsureCompletedOnField()
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
        public int Foo(Task<int> task)
        {
            return /*MM*/_task.EnsureCompleted();
        }

        private Task<int> _task = Task.FromResult(42);
    }
}
" + TaskExtensionsString);

            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            var diagnostic = Assert.Single(diagnostics);
            
            Assert.Equal("AZC0104", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostic.Location);
        }

        [Fact]
        public async Task AZC0104WarningEnsureCompletedOnVariable()
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
        public static async Task FooAsync()
        {
            Func<int, int> a = i => 
            {
                Task<int> task = Task.FromResult(0);
                task = FooImplAsync();
                return /*MM*/task.EnsureCompleted();
            };
        }

        private static async Task<int> FooImplAsync(CancellationToken ct = default(CancellationToken)) => 42;
    }
}
" + TaskExtensionsString);

            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            var diagnostic = Assert.Single(diagnostics);
            
            Assert.Equal("AZC0104", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostic.Location);
        }

        [Fact]
        public async Task AZC0104WarningEnsureCompletedOnParameter()
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
        public static int Foo(Task<int> task)
        {
            return /*MM*/task.EnsureCompleted();
        }

        private static async Task<int> FooImplAsync(CancellationToken ct = default(CancellationToken)) => 42;
    }
}
" + TaskExtensionsString);

            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            var diagnostic = Assert.Single(diagnostics);
            
            Assert.Equal("AZC0104", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostic.Location);
        }

        [Fact]
        public async Task AZC0104WarningEnsureCompletedOnProperty()
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
        public static int Foo(Task<int> task)
        {
            return /*MM*/FooImplAsync.EnsureCompleted();
        }

        private static Task<int> FooImplAsync => Task.FromResult(42);
    }
}
" + TaskExtensionsString);

            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            var diagnostic = Assert.Single(diagnostics);
            
            Assert.Equal("AZC0104", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostic.Location);
        }
    }
}