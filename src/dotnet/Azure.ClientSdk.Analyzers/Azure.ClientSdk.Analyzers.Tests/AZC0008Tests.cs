// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.ClientOptionsAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0008Tests
    {
        [Fact]
        public async Task AZC0008ProducedForClientOptionsWithoutServiceVersionEnum()
        {
            const string code = @"
namespace RandomNamespace
{
    public class {|AZC0008:SomeClientOptions|} : Azure.Core.ClientOptions { 

    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0008NotProducedForClientOptionsWithServiceVersionEnum()
        {
            const string code = @"
namespace RandomNamespace
{
    public class SomeClientOptions { 

        public enum ServiceVersion
        {
            V2018_11_09 = 0
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0009")
                .RunAsync();
        }
    }
}
