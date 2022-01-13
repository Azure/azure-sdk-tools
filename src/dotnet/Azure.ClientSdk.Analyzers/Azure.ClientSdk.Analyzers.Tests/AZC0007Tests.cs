// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.ClientConstructorAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0007Tests
    {
        [Fact]
        public async Task AZC0007ProducedForClientsWithoutOptionsCtor()
        {
            const string code = @"
namespace RandomNamespace
{
    public class SomeClient
    {
        public {|AZC0007:SomeClient|}() {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0007ProducedForClientsWithoutOptionsCtorWithArguments()
        {
            const string code = @"
namespace RandomNamespace
{
    public class SomeClient
    {
        protected SomeClient() {}
        public {|AZC0007:SomeClient|}(string connectionString) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0007ProducedForClientsWithOptionsNotDerivedFromClientOptions()
        {
            const string code = @"
namespace RandomNamespace
{
    public class SomeClientOptions { }

    public class SomeClient
    {
        protected SomeClient() {}
        public {|AZC0007:SomeClient|}(string connectionString, SomeClientOptions options) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0007NotProducedForClientsWithDefaultOptionsCtorWithArguments()
        {
            const string code = @"
namespace RandomNamespace
{
    using System;

    public class SomeClientOptions : Azure.Core.ClientOptions {}

    public class SomeClient
    {
        protected SomeClient() {}
        public SomeClient(string connectionString, SomeClientOptions clientOptions = default) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }
    }
}