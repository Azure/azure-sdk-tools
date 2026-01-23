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

        // This test validates that the analyzer does not produce a diagnostic for clients with multiple constructors
        // which end with a ClientOptions parameter.
        [Fact]
        public async Task AZC0007NotProducedForClientWithMultipleCtors()
        {
            const string code = @"
using System;
using Azure;
using Azure.Core;

namespace RandomNamespace.Foo
{

    public partial class RoomsClientOptions : ClientOptions {} 

    public partial class RoomsClient
    {
        protected RoomsClient() {}
        public RoomsClient(string connectionString) {}
        public RoomsClient(string connectionString, RoomsClientOptions options) {}
        public RoomsClient(Uri endpoint, AzureKeyCredential credential, RoomsClientOptions options = null) {}
        public RoomsClient(Uri endpoint, TokenCredential credential, RoomsClientOptions options = null) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0007NotProducedForClientWithClientSettings()
        {
            const string code = @"
namespace System.ClientModel
{
    public class ClientSettings {}
}

namespace RandomNamespace
{
    public class SomeClientSettings : System.ClientModel.ClientSettings {}

    public class SomeClient
    {
        protected SomeClient() {}
        public SomeClient(SomeClientSettings settings) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0005ProducedForClientWithOnlyClientSettingsAndNoParameterlessCtor()
        {
            const string code = @"
namespace System.ClientModel
{
    public class ClientSettings {}
}

namespace RandomNamespace
{
    public class SomeClientSettings : System.ClientModel.ClientSettings {}

    public class {|AZC0005:SomeClient|}
    {
        public SomeClient(SomeClientSettings settings) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0007NotProducedForClientWithClientSettingsAndOtherOverloads()
        {
            const string code = @"
namespace System.ClientModel
{
    public class ClientSettings {}
}

namespace RandomNamespace
{
    using Azure.Core;

    public class SomeClientOptions : ClientOptions {}
    public class SomeClientSettings : System.ClientModel.ClientSettings {}

    public class SomeClient
    {
        protected SomeClient() {}
        public SomeClient(string connectionString) {}
        public SomeClient(string connectionString, SomeClientOptions options) {}
        public SomeClient(SomeClientSettings settings) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0007ProducedForClientWithClientSettingsButMissingOptionsOverload()
        {
            const string code = @"
namespace System.ClientModel
{
    public class ClientSettings {}
}

namespace RandomNamespace
{
    public class SomeClientSettings : System.ClientModel.ClientSettings {}

    public class SomeClient
    {
        protected SomeClient() {}
        public {|AZC0007:SomeClient|}(string connectionString) {}
        public SomeClient(SomeClientSettings settings) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0007NotProducedForClientWithMultipleClientSettingsOverloads()
        {
            const string code = @"
namespace System.ClientModel
{
    public class ClientSettings {}
}

namespace RandomNamespace
{
    using System;
    using Azure;

    public class SomeClientSettings : System.ClientModel.ClientSettings {}
    public class AnotherClientSettings : System.ClientModel.ClientSettings {}

    public class SomeClient
    {
        protected SomeClient() {}
        public SomeClient(SomeClientSettings settings) {}
        public SomeClient(AnotherClientSettings settings) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0007ProducedForClientsWithSettingsNotDerivedFromClientSettings()
        {
            const string code = @"
namespace RandomNamespace
{
    public class SomeClientSettings { }

    public class SomeClient
    {
        protected SomeClient() {}
        public {|AZC0007:SomeClient|}(SomeClientSettings settings) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }
    }
}
