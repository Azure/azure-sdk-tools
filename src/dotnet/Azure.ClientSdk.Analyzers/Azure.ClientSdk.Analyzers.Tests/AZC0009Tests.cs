// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.ClientOptionsAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0009Tests
    {
        [Fact]
        public async Task AZC0009ProducedForClientOptionsWithOnlyDefaultCtor()
        {
            const string code = @"
namespace RandomNamespace
{
    public class {|AZC0009:SomeClientOptions|} : Azure.Core.ClientOptions {

        public enum ServiceVersion
        {
            V2018_11_09 = 0
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0009ProducedForClientOptionsWithoutServiceVersionInCtor()
        {
            const string code = @"
namespace RandomNamespace
{
    public class SomeClientOptions : Azure.Core.ClientOptions {

        public enum ServiceVersion
        {
            V2018_11_09 = 0
        }

        public {|AZC0009:SomeClientOptions|}()
        {
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0009ProducedForClientOptionsCtorWhereServiceVersionNotFirstParam()
        {
            const string code = @"
namespace RandomNamespace
{
    public class SomeClientOptions : Azure.Core.ClientOptions { 

        public enum ServiceVersion
        {
            V2018_11_09 = 0
        }

        public SomeClientOptions(string {|AZC0009:anOption|}, ServiceVersion version = ServiceVersion.V2018_11_09)
        {
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0009NotProducedForClientOptionsCtorWhereServiceVersionFirstParam()
        {
            const string code = @"
namespace RandomNamespace
{
    public class SomeClientOptions { 

        public enum ServiceVersion
        {
            V2018_11_09 = 0
        }

        public SomeClientOptions(ServiceVersion version = ServiceVersion.V2018_11_09)
        {
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }
    }
}
