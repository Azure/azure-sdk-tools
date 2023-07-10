// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.ClientMethodsAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0017Tests
    {
        [Fact]
        public async Task AZC0017ProducedForMethodsWithRequestContentParameter()
        {
            const string code = @"
using Azure.Core;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task {|AZC0017:GetAsync|}(RequestContent content, CancellationToken cancellationToken = default)
        {
            return null;
        }

        public virtual void {|AZC0017:Get|}(RequestContent content, CancellationToken cancellationToken = default)
        {
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0017NotProducedForMethodsWithCancellationToken()
        {
            const string code = @"
using Azure.Core;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task GetAsync(string s, CancellationToken cancellationToken = default)
        {
            return null;
        }

        public virtual void Get(string s, CancellationToken cancellationToken = default)
        {
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .RunAsync();
        }
    }
}
