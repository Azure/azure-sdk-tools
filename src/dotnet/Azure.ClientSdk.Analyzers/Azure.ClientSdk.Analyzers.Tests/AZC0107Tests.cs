// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;

namespace Azure.ClientSdk.Analyzers.Tests 
{
    public class AZC0107Tests 
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
        public async Task AZC0107WarningInAsyncMethodFalseValue()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core.Pipeline;
    public class MyClass
    {
        public static async Task FooAsync()
        {
            await FooImplAsync(/*MM*/false).ConfigureAwait(false);
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
            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0107", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostic.Location);
        }

        [Fact]
        public async Task AZC0107WarningInAsyncScopeFalseValue()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core.Pipeline;
    public class MyClass
    {
        private static async Task FooAsync(bool async)
        {
            if (async)
            {
                await FooImplAsync(/*MM*/false).ConfigureAwait(false);
            }
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
            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0107", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostic.Location);
        }

        [Fact]
        public async Task AZC0107WarningInAsyncLambdaFalseValue()
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
        private static void Foo(bool async)
        {
            Func<Task<int>> fooAsync = async () 
                => async ? await FooImplAsync(/*MM*/false).ConfigureAwait(false) : 42;
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
            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0107", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostic.Location);
        }

        [Fact]
        public async Task AZC0107WarningInSyncMethodTrueValue()
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
            FooImplAsync(/*MM*/true).EnsureCompleted();
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
            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0107", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostic.Location);
        }

        [Fact]
        public async Task AZC0107WarningInSyncPropertyTrueValue()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core.Pipeline;
    public class MyClass
    {
        public static int Foo { get { return FooImplAsync(/*MM*/true).EnsureCompleted(); } }

        private static async Task<int> FooImplAsync(bool async, CancellationToken ct = default(CancellationToken)) 
        {
            await Task.Yield();
            return 42;
        }
    }
}
" + TaskExtensionsString);

            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0107", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostic.Location);
        }

        [Fact]
        public async Task AZC0107WarningInSyncExpressionPropertyTrueValue()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core.Pipeline;
    public class MyClass
    {
        public static int Foo => FooImplAsync(/*MM*/true).EnsureCompleted();

        private static async Task<int> FooImplAsync(bool async, CancellationToken ct = default(CancellationToken)) 
        {
            await Task.Yield();
            return 42;
        }
    }
}
" + TaskExtensionsString);

            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0107", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostic.Location);
        }

        [Fact]
        public async Task AZC0107WarningInSyncScopeTrueValue()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core.Pipeline;
    public class MyClass
    {
        private static async Task FooAsync(bool async)
        {
            if (!async)
            {
                FooImplAsync(/*MM*/true).EnsureCompleted();
            }
            else 
            {
                await Task.Yield();
            }
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
            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0107", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostic.Location);
        }

        [Fact]
        public async Task AZC0107WarningInSyncLambdaTrueValue()
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
        private static async Task FooAsync(bool async)
        {
            Func<Task<int>> fooAsync = async () 
                => async ? await FooImplAsync(true).ConfigureAwait(false) : FooImplAsync(/*MM*/true).EnsureCompleted();
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
            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0107", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostic.Location);
        }

        [Fact]
        public async Task AZC0107NoWarningInAsyncMethodTrueValue()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core.Pipeline;
    public class MyClass
    {
        public static async Task FooAsync()
        {
            await FooImplAsync(true).ConfigureAwait(false);
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

        [Fact]
        public async Task AZC0107NoWarningInAsyncScopeTrueValue()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core.Pipeline;
    public class MyClass
    {
        private static async Task FooAsync(bool async)
        {
            if (async)
            {
                await FooImplAsync(true).ConfigureAwait(false);
            }
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

        [Fact]
        public async Task AZC0107NoWarningInAsyncLambdaTrueValue()
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
        private static void Foo(bool async)
        {
            Func<Task<int>> fooAsync = async () 
                => async ? await FooImplAsync(true).ConfigureAwait(false) : 42;
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

        [Fact]
        public async Task AZC0107NoWarningInSyncMethodFalseValue()
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

        [Fact]
        public async Task AZC0107NoWarningInSyncScopeFalseValue()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core.Pipeline;
    public class MyClass
    {
        private static async Task FooAsync(bool async)
        {
            if (!async)
            {
                FooImplAsync(false).EnsureCompleted();
            }
            else 
            {
                await Task.Yield();
            }
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

        [Fact]
        public async Task AZC0107NoWarningInSyncLambdaFalseValue()
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
        private static async Task FooAsync(bool async)
        {
            Func<Task<int>> fooAsync = async () 
                => async ? await FooImplAsync(true).ConfigureAwait(false) : FooImplAsync(false).EnsureCompleted();
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