// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.ClientOptionsAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0010Tests
    {
        [Fact]
        public async Task AZC0010ProducedForClientOptionsCtorWithNoServiceVersionDefault()
        {
            const string code = @"
namespace RandomNamespace
{
    public class SomeClientOptions { 

        public enum ServiceVersion
        {
            V2018_11_09 = 0
        }

        public SomeClientOptions(ServiceVersion [|version|])
        {
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, "AZC0010");
        }

        [Fact]
        public async Task AZC0010ProducedForClientOptionsCtorWithNonMaxVersionExplicit()
        {
            const string code = @"
namespace RandomNamespace
{
    public class SomeClientOptions { 

        public enum ServiceVersion
        {
            V2018_11_09 = 0,
            V2019_03_20 = 1
        }

        public SomeClientOptions(ServiceVersion [|version|] = ServiceVersion.V2018_11_09)
        {
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, "AZC0010");
        }

        [Fact]
        public async Task AZC0010ProducedForClientOptionsCtorWithNonMaxVersionImplicit()
        {
            const string code = @"
namespace RandomNamespace
{
    public class SomeClientOptions { 

        public enum ServiceVersion
        {
            V2018_11_09,
            V2019_03_20
        }

        public SomeClientOptions(ServiceVersion [|version|] = ServiceVersion.V2018_11_09)
        {
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, "AZC0010");
        }

        [Fact]
        public async Task AZC0010NotProducedForClientOptionsCtorWithMaxServiceVersionDefault()
        {
            const string code = @"
namespace RandomNamespace
{
    public class SomeClientOptions { 

        public enum ServiceVersion
        {
            V2018_11_09 = 0,
            V2019_03_20 = 1
        }

        public SomeClientOptions(ServiceVersion version = ServiceVersion.V2019_03_20)
        {
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }
    }
}
