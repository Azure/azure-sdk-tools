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

        // This test validates that AZC0008 is produced for nested client options types
        // when both types do not have a service version enum.
        [Fact]
        public async Task AZC0008ProducedForAllNestedClientOptionsTypeWithoutServiceVersionEnum()
        {
            const string code = @"
namespace RandomNamespace
{
    public class {|AZC0008:SomeBaseClientOptions|} : Azure.Core.ClientOptions
    {
    }
    public class {|AZC0008:SomeClientOptions|} : SomeBaseClientOptions { 

    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }


        [Fact]
        public async Task AZC0008ProducedForNestedClientOptionsTypeWithoutServiceVersionEnum()
        {
            const string code = @"
namespace RandomNamespace
{
    public class {|AZC0008:SomeBaseClientOptions|} : Azure.Core.ClientOptions
    {
    }
    public class SomeClientOptions : SomeBaseClientOptions { 
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

        // This test validates that AZC0008 is not produced for nested client options types
        // when both types define a service version enum.
        [Fact]
        public async Task AZC0008NotProducedForAllNestedClientOptionsTypeWithoutServiceVersionEnum()
        {
            const string code = @"
using Azure;
using Azure.Core;

namespace RandomNamespace
{
    public class SomeBaseClientOptions : ClientOptions
    {
        public enum ServiceVersion
        {
            V2018_11_09 = 0
        }
    }
    public class SomeClientOptions : SomeBaseClientOptions { 
        public new enum ServiceVersion
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
