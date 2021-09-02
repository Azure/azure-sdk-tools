// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.AsyncAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests 
{
    public class AZC0105Tests 
    {
        [Fact]
        public async Task AZC0105WarningPublicAsyncMethodWithAsyncParameter() {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    public class MyClass
    {
        public static async Task<int> FooAsync({|AZC0105:bool async|}, CancellationToken ct)
            => async ? await Task.FromResult(42).ConfigureAwait(false) : 42;
    }
}";

            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0105WarningPublicSyncMethodWithAsyncParameter()
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    public class MyClass
    {
        public static void Foo({|AZC0105:bool async|}, CancellationToken ct) { }
    }
}";

            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0105NoWarningInternalAsyncMethodWithAsyncParameter()
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    internal class MyClass
    {
        public static async Task<int> FooAsync(bool async, CancellationToken ct)
            => async ? await Task.FromResult(42).ConfigureAwait(false) : 42;
    }
}";

            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0105NoWarningPrivateSyncMethodWithAsyncParameter()
        {
            const string code = @"
namespace RandomNamespace
{
    using System.Threading;
    using System.Threading.Tasks;
    public class MyClass
    {
        private void Foo(bool async, CancellationToken ct) { }
    }
}";

            await Verifier.VerifyAnalyzerAsync(code);
        }
    }
}