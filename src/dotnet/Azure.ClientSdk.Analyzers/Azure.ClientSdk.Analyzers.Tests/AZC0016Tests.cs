// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.ClientOptionsAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0016Tests
    {
        // Testing multiple cases together to also make sure all violations are found at once.

        [Fact]
        public async Task AZC0016ProducedForInvalidServiceVersionEnumNames()
        {
            const string code = @"
namespace RandomNamespace
{
    public class SomeClientOptions : Azure.Core.ClientOptions
    {
        public enum ServiceVersion
        {
            {|AZC0016:v2021_05_01|} = 1,
            {|AZC0016:V2021_07_15_preview|} = 2,
            {|AZC0016:V2021_07_15__Preview|} = 2,
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0009")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0016NotProducedForValidServiceVersionEnumNames()
        {
            const string code = @"
namespace RandomNamespace
{
    public class SomeClientOptions
    {
        public enum ServiceVersion
        {
            V2021_05_01 = 1,
            V2021_07_15_Preview,
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0009")
                .RunAsync();
        }
    }
}
