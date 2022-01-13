// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.ClientConstructorAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0005Tests
    {
        [Fact]
        public async Task AZC0005ProducedForClientTypesWithoutProtectedCtor()
        {
            const string code = @"
namespace RandomNamespace
{
    public class SomeClientOptions : Azure.Core.ClientOptions { }

    public class {|AZC0005:SomeClient|}
    {
        public SomeClient(string connectionString) {}
        public SomeClient(string connectionString, SomeClientOptions options) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }
    }
}