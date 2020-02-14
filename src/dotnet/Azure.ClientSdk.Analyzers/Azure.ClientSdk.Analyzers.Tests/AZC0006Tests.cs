// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.ClientConstructorAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0006Tests
    {
        [Fact]
        public async Task AZC0006ProducedForClientsWithoutOptionsCtor()
        {
            const string code = @"
namespace RandomNamespace
{
    public class SomeClientOptions { }

    public class SomeClient
    {
        protected SomeClient() {}
        public [|SomeClient|](SomeClientOptions options) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, "AZC0006");
        }

        [Fact]
        public async Task AZC0006ProducedForClientsWithoutOptionsCtorWithArguments()
        {
            const string code = @"
namespace RandomNamespace
{
    public class SomeClientOptions { }

    public class SomeClient
    {
        protected SomeClient() {}
        public [|SomeClient|](string connectionString, SomeClientOptions options) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, "AZC0006");
        }
    }
}