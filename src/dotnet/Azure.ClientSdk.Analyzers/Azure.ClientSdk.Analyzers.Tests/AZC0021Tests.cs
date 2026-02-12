// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.ClientConstructorAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0021Tests
    {
        [Fact]
        public async Task AZC0021ProducedForClientSettingsWithOtherParameters()
        {
            const string code = @"
namespace System.ClientModel.Primitives
{
    public class ClientSettings {}
}

namespace RandomNamespace
{
    public class SomeClientSettings : System.ClientModel.Primitives.ClientSettings {}

    public class SomeClient
    {
        protected SomeClient() {}
        public {|AZC0021:SomeClient|}(string connectionString, SomeClientSettings settings) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0021ProducedForClientSettingsWithMultipleOtherParameters()
        {
            const string code = @"
namespace System.ClientModel.Primitives
{
    public class ClientSettings {}
}

namespace RandomNamespace
{
    using System;
    using Azure;

    public class SomeClientSettings : System.ClientModel.Primitives.ClientSettings {}

    public class SomeClient
    {
        protected SomeClient() {}
        public {|AZC0021:SomeClient|}(Uri endpoint, AzureKeyCredential credential, SomeClientSettings settings) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0021NotProducedForClientSettingsAsOnlyParameter()
        {
            const string code = @"
namespace System.ClientModel.Primitives
{
    public class ClientSettings {}
}

namespace RandomNamespace
{
    public class SomeClientSettings : System.ClientModel.Primitives.ClientSettings {}

    public class SomeClient
    {
        protected SomeClient() {}
        public SomeClient(SomeClientSettings settings) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0021NotProducedForNonClientSettingsWithMultipleParameters()
        {
            const string code = @"
namespace RandomNamespace
{
    using Azure.Core;

    public class SomeClientOptions : ClientOptions {}

    public class SomeClient
    {
        protected SomeClient() {}
        public SomeClient(string connectionString) {}
        public SomeClient(string connectionString, SomeClientOptions options) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0021ProducedForClientSettingsInMiddlePosition()
        {
            const string code = @"
namespace System.ClientModel.Primitives
{
    public class ClientSettings {}
}

namespace RandomNamespace
{
    public class SomeClientSettings : System.ClientModel.Primitives.ClientSettings {}

    public class SomeClient
    {
        protected SomeClient() {}
        public {|AZC0021:SomeClient|}(SomeClientSettings settings, string connectionString) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }
    }
}
