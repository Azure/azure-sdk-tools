// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.AsyncAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests 
{
    public class AZC0102Tests 
    {
        [Theory]
        [InlineData(LanguageVersion.CSharp7)]
        [InlineData(LanguageVersion.Latest)]
        public async Task AZC0102WarningOnTask(LanguageVersion languageVersion) 
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading.Tasks;
    public class MyClass
    {
        public static void Foo()
        {
            Task<int> task = Task.Run(() => 10);
            task.{|AZC0102:GetAwaiter().GetResult()|};
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, languageVersion);
        }

        [Theory]
        [InlineData(LanguageVersion.CSharp7)]
        [InlineData(LanguageVersion.Latest)]
        public async Task AZC0102WarningOnValueTask(LanguageVersion languageVersion)
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading.Tasks;
    public class MyClass
    {
        public static void Foo()
        {
            new ValueTask().{|AZC0102:GetAwaiter().GetResult()|};
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, languageVersion);
        }

        [Fact]
        public async Task AZC0102WarningOnAwaitable()
        {
            const string code = @"
namespace RandomNamespace
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;

    public class MyClass
    {
        public static void Foo()
        {
            new CustomAwaitable().{|AZC0102:GetAwaiter().GetResult()|};
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
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0102WarningOnExtension()
        {
            const string code = @"
namespace RandomNamespace
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;

    public class MyClass
    {
        public static void Foo()
        {
            new TestStruct().{|AZC0102:GetAwaiter().GetResult()|};
        }
    }

    public struct TestStruct { }

    public static class Extensions
    {
        public static TaskAwaiter GetAwaiter(this TestStruct s) => default;
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0102NoWarningOnNonAwaiter()
        {
            const string code = @"
namespace RandomNamespace
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    
    public class MyClass
    {
        public static void Foo()
        {
            new CustomAwaitable().GetAwaiter().GetResult();
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
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0102NoWarningOnNonAwaitable()
        {
            const string code = @"
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
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }
    }
}