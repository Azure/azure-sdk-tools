// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;

namespace Azure.ClientSdk.Analyzers.Tests 
{
    public class AZC0011Tests 
    {
        private readonly DiagnosticAnalyzerRunner _runner = new DiagnosticAnalyzerRunner(new AddConfigureAwaitAnalyzer());

        [Fact]
        public async Task AZC0011WarningOnTask()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class MyClass
    {
        public static async System.Threading.Tasks.Task Foo()
        {
            /*MM*/await System.Threading.Tasks.Task.Run(() => {});
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0011", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0011WarningOnTaskOfT() 
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class MyClass
    {
        public static async System.Threading.Tasks.Task Foo()
        {
            var i = /*MM*/await System.Threading.Tasks.Task.Run(() => 42);
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0011", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0011WarningOnValueTask() 
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class MyClass
    {
        private static int _x;

        public static async System.Threading.Tasks.ValueTask Foo()
        {
            /*MM*/await RunAsync();
        }

        private static async System.Threading.Tasks.ValueTask RunAsync()
        {
            _x++;
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0011", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0011WarningOnValueTaskOfT() 
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class MyClass
    {
        public static async System.Threading.Tasks.ValueTask Foo()
        {
            var i = /*MM*/await GetValueAsync();
        }

        private static async System.Threading.Tasks.ValueTask<int> GetValueAsync()
        {
            return 0;
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0011", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact(Skip = "It isn't clear if ConfigureAwait(true) should be treated as error")]
        public async Task AZC0011WarningOnExistingConfigureAwaitTrue()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class MyClass
    {
        public static async System.Threading.Tasks.Task Foo()
        {
           /*MM*/await System.Threading.Tasks.Task.Run(() => {}).ConfigureAwait(true);
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0011", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0011NoWarningOnExistingConfigureAwaitFalse()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class MyClass
    {
        public static async System.Threading.Tasks.Task Foo()
        {
           await System.Threading.Tasks.Task.Run(() => {}).ConfigureAwait(false);
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task AZC0011WarningOnTaskDelay()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class MyClass
    {
        public static async System.Threading.Tasks.Task Foo()
        {
            /*MM*/await System.Threading.Tasks.Task.Delay(42);
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0011", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0011NoWarningOnTaskYield()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class MyClass
    {
        public static async System.Threading.Tasks.Task Foo()
        {
           await System.Threading.Tasks.Task.Yield();
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task AZC0011WarningOnVariable()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class MyClass
    {
        public static async System.Threading.Tasks.Task Foo()
        {
            var task = System.Threading.Tasks.Task.Run(() => {});
            /*MM*/await task;
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0011", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }
    }
}