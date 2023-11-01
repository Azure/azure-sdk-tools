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
    public class SomeClientOptions : Azure.Core.ClientOptions { }

    public class SomeClient
    {
        protected SomeClient() {}
        public {|AZC0006:SomeClient|}(SomeClientOptions options) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0006ProducedForClientsWithoutOptionsCtorWithArguments()
        {
            const string code = @"
namespace RandomNamespace
{
    public class SomeClientOptions : Azure.Core.ClientOptions { }

    public class SomeClient
    {
        protected SomeClient() {}
        public {|AZC0006:SomeClient|}(string connectionString, SomeClientOptions options) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0006NotProducedForClientsWithoutOptionsCtorWithArguments()
        {
            const string code = @"
namespace RandomNamespace
{
    public class SomeClientOptions : Azure.Core.ClientOptions { }

    public class SomeClient
    {
        protected SomeClient() {}
        public SomeClient(string connectionString, SomeClientOptions options = null) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0006NotProducedForSharedClientOptions()
        {
            const string code = @"
namespace RandomNamespace
{
    public class SharedClientOptions : Azure.Core.ClientOptions { }

    public class SomeClient
    {
        protected SomeClient() {}
        public SomeClient(string connectionString, SharedClientOptions options = null) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0006NotProducedForClientsOptions()
        {
            const string code = @"
namespace RandomNamespace
{
    public class SomeClientsOptions : Azure.Core.ClientOptions { } // Type has 'ClientsOptions' suffix instead of 'ClientOptions'

    public class SomeClient
    {
        protected SomeClient() {}
        public SomeClient(string connectionString, SomeClientsOptions options = null) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0006NotProducedForClientsWithStaticProperties()
        {
            const string code = @"
namespace RandomNamespace
{
    public class SomeClientOptions : Azure.Core.ClientOptions { }

    public class SomeClient
    {
        public static int a = 1;
        public SomeClient() {}
        public SomeClient(SomeClientOptions options) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }
    }
}
