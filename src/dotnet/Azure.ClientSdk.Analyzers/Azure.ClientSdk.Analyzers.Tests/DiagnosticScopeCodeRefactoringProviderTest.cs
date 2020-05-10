// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureRefactoringVerifier<Azure.ClientSdk.Analyzers.DiagnosticScopeCodeRefactoringProvider>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class DiagnosticScopeCodeRefactoringProviderTest
    {
        private static string DiagnosticFramework = @"
using System;
namespace Azure.Core.Pipeline
{
    using System;
    internal readonly struct DiagnosticScope : IDisposable
    {
        public void Start() { }

        public void Dispose() { }

        public void Failed(Exception e) { }
    }

    internal sealed partial class ClientDiagnostics
    { 
        public DiagnosticScope CreateScope(string name)
        {
            return default;   
        }
    }
}
";

        [Fact]
        public async Task AddsScopeToSyncMethod()
        {
            const string code = @"
using System;
using Azure.Core.Pipeline;
namespace RandomNamespace
{
    public class MyClass
    {
        ClientDiagnostics _clientDiagnostics;
        public void [||]Foo(int b)
        {
            if (b == 0) throw new ArgumentException(nameof(b));
            int a = 2 + b;
        }
    }
}";
            const string fixedCode = @"
using System;
using Azure.Core.Pipeline;
namespace RandomNamespace
{
    public class MyClass
    {
        ClientDiagnostics _clientDiagnostics;
        public void Foo(int b)
        {
            if (b == 0) throw new ArgumentException(nameof(b));
            using DiagnosticScope scope = _clientDiagnostics.CreateScope($""{nameof(MyClass)}.{nameof(Foo)}"");
            scope.Start();
            try
            {
                int a = 2 + b;
            }
            catch (Exception ex)
            {
                scope.Failed(ex);
                throw;
            }
        }
    }
}";

            await Verifier.CreateRefactoring(code, fixedCode)
                .WithSources(DiagnosticFramework)
                .RunAsync();
        }

        [Fact]
        public async Task AddsScopeToExpressionSyncMethod()
        {
            const string code = @"
using System;
using Azure.Core.Pipeline;
namespace RandomNamespace
{
    public class MyClass
    {
        ClientDiagnostics _clientDiagnostics;
        public int [||]Foo(int b) => b + 2;
    }
}";
            const string fixedCode = @"
using System;
using Azure.Core.Pipeline;
namespace RandomNamespace
{
    public class MyClass
    {
        ClientDiagnostics _clientDiagnostics;
        public int Foo(int b)
        {
            using DiagnosticScope scope = _clientDiagnostics.CreateScope($""{nameof(MyClass)}.{nameof(Foo)}"");
            scope.Start();
            try
            {
                return b + 2;
            }
            catch (Exception ex)
            {
                scope.Failed(ex);
                throw;
            }
        }
    }
}";

            await Verifier.CreateRefactoring(code, fixedCode)
                .WithSources(DiagnosticFramework)
                .RunAsync();
        }

        [Fact]
        public async Task AddsScopeToAsyncMethod()
        {
            const string code = @"
using System;
using System.Threading.Tasks;
using Azure.Core.Pipeline;
namespace RandomNamespace
{
    public class MyClass
    {
        ClientDiagnostics _clientDiagnostics;

        public async void Foo(int b) {}
        public async Task [||]FooAsync(int b)
        {
            if (b == 0) throw new ArgumentException(nameof(b));
            int a = 2 + b;
        }
    }
}";
            const string fixedCode = @"
using System;
using System.Threading.Tasks;
using Azure.Core.Pipeline;
namespace RandomNamespace
{
    public class MyClass
    {
        ClientDiagnostics _clientDiagnostics;

        public async void Foo(int b) {}
        public async Task [||]FooAsync(int b)
        {
            if (b == 0) throw new ArgumentException(nameof(b));
            using DiagnosticScope scope = _clientDiagnostics.CreateScope($""{nameof(MyClass)}.{nameof(Foo)}"");
            scope.Start();
            try
            {
                int a = 2 + b;
            }
            catch (Exception ex)
            {
                scope.Failed(ex);
                throw;
            }
        }
    }
}";

            await Verifier.CreateRefactoring(code, fixedCode)
                .WithSources(DiagnosticFramework)
                .RunAsync();
        }
    }
}