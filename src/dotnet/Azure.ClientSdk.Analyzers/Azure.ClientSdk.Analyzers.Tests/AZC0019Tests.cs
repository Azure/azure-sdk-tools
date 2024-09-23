// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.ClientMethodsAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0019Tests
    {
        [Fact]
        public async Task AZC0019ProducedForMethodsWithNoRequestContentButProtocolAndConvenience()
        {
            const string code = @"
using Azure;
using Azure.Core;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task<Response> GetAsync(string a, CancellationToken cancellationToken = default)
        {
            return null;
        }

        public virtual Response Get(string a, CancellationToken cancellationToken = default)
        {
            return null;
        }

        public virtual Task<Response> {|AZC0019:GetAsync|}(string a, Azure.RequestContext context = null)
        {
            return null;
        }

        public virtual Response {|AZC0019:Get|}(string a, Azure.RequestContext context = null)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .RunAsync();
        }
    }
}
