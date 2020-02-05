// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Azure.ClientSdk.Analyzers.Tests 
{
    public class AZC0102Tests 
    {
        private readonly DiagnosticAnalyzerRunner _runner = new DiagnosticAnalyzerRunner(new AsyncAnalyzer());

        [Theory]
        [InlineData(LanguageVersion.CSharp7)]
        [InlineData(LanguageVersion.Latest)]
        public async Task AZC0102WarningOnTask(LanguageVersion version)
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System.Threading.Tasks;
    public class MyClass
    {
        public static void Foo()
        {
            Task<int> task = Task.Run(() => 10);
            task./*MM*/GetAwaiter().GetResult();
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source, version);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0102", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostic.Location);
        }

        [Theory]
        [InlineData(LanguageVersion.CSharp7)]
        [InlineData(LanguageVersion.Latest)]
        public async Task AZC0102WarningOnValueTask(LanguageVersion version)
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System.Threading.Tasks;
    public class MyClass
    {
        public static void Foo()
        {
            new ValueTask()./*MM*/GetAwaiter().GetResult();
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source, version);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0102", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostic.Location);
        }

        [Fact]
        public async Task AZC0102WarningOnAwaitable()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;

    public class MyClass
    {
        public static void Foo()
        {
            new CustomAwaitable()./*MM*/GetAwaiter().GetResult();
        }
    }

    public class CustomAwaitable
    {
        public CustomAwaiter GetAwaiter() => new CustomAwaiter();
    }

    public class CustomAwaiter : ICriticalNotifyCompletion
    {
        internal bool IsCompleted => true;
        protected internal void GetResult() {}
        public void OnCompleted(Action continuation) {}
        public void UnsafeOnCompleted(Action continuation) {}
    }  
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0102", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostic.Location);
        }

        [Fact]
        public async Task AZC0102WarningOnExtension()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;

    public class MyClass
    {
        public static void Foo()
        {
            new TestStruct()./*MM*/GetAwaiter().GetResult();
        }
    }

    public struct TestStruct { }

    public static class Extensions
    {
        public static TaskAwaiter GetAwaiter(this TestStruct s) => default;
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0102", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostic.Location);
        }

        [Fact]
        public async Task AZC0102NoWarningOnNonAwaiter()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    
    public class MyClass
    {
        public static void Foo()
        {
            new CustomAwaitable()./*MM*/GetAwaiter().GetResult();
        }
    }

    public class CustomAwaitable
    {
        public FakeAwaiter GetAwaiter() => new FakeAwaiter();
    }

    public class FakeAwaiter : INotifyCompletion 
    {
        protected bool IsCompleted => true;
        public void GetResult() {}
        public void OnCompleted(Action continuation) {}
    } 
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task AZC0102NoWarningOnNonAwaitable()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    
    public class MyClass
    {
        public static void Foo()
        {
            new NonAwaitable().GetAwaiter().GetResult();
        }
    }

    public class NonAwaitable
    {
        public TaskAwaiter GetAwaiter(int i = default) => default(TaskAwaiter);
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            Assert.Empty(diagnostics);
        }
    }
}