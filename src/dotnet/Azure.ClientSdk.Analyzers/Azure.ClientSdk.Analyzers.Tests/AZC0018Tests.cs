// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.ClientMethodsAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0018Tests
    {
        [Fact]
        public async Task AZC0018NotProducedForMethodsWithRequestContext()
        {
            const string code = @"
using Azure;
using Azure.Core;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task<Response> GetAsync(string s, RequestContext context = null)
        {
            return null;
        }

        public virtual Response Get(string s, RequestContext context = null)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0018ProducedForMethodsWithGenericResponse()
        {
            const string code = @"
using Azure;
using Azure.Core;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task<Response<string>> {|AZC0018:GetAsync|}(string s, RequestContext context = null)
        {
            return null;
        }

        public virtual Response<string> {|AZC0018:Get|}(string s, RequestContext context = null)
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
