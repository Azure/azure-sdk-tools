// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.ClientMethodsAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0002Tests
    {
        [Fact]
        public async Task AZC0002ProducedForMethodsWithoutCancellationTokenOrRequestContext()
        {
            const string code = @"
using Azure;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task<Response> {|AZC0002:GetAsync|}()
        {
            return null;
        }

        public virtual Response {|AZC0002:Get|}()
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0002ProducedForMethodsWithWrongNameCancellationToken()
        {
            const string code = @"
using Azure;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task<Response> {|AZC0002:GetAsync|}(CancellationToken cancellation = default)
        {
            return null;
        }

        public virtual Response {|AZC0002:Get|}(CancellationToken cancellation = default)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0002ProducedForMethodsWithWrongNameRequestContext()
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
        public virtual Task<Response> {|AZC0002:GetAsync|}(RequestContext cancellation = default)
        {
            return null;
        }

        public virtual Response {|AZC0002:Get|}(RequestContext cancellation = default)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0002ProducedForMethodsWithNonOptionalCancellationToken()
        {
            const string code = @"
using Azure;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task<Response> {|AZC0002:GetAsync|}(CancellationToken cancellationToken)
        {
            return null;
        }

        public virtual Response {|AZC0002:Get|}(CancellationToken cancellationToken)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0002ProducedForMethodsWhereRequestContextIsNotLast()
        {
            const string code = @"
using Azure;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task<Response> {|AZC0002:GetAsync|}(RequestContext context = default, string text = default)
        {
            return null;
        }

        public virtual Response {|AZC0002:Get|}(RequestContext context = default, string text = default)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0002ProducedForMethodsWhereCancellationTokenIsNotLast()
        {
            const string code = @"
using Azure;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task<Response> {|AZC0002:GetAsync|}(CancellationToken cancellationToken = default, string text = default)
        {
            return null;
        }

        public virtual Response {|AZC0002:Get|}(CancellationToken cancellationToken = default, string text = default)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0002NotProducedForMethodsWithCancellationToken()
        {
            const string code = @"
using Azure;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task<Response> GetAsync(CancellationToken cancellationToken = default)
        {
            return null;
        }

        public virtual Response Get(CancellationToken cancellationToken = default)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0002NotProducedForMethodsWithRequestContextAndCancellationToken()
        {
            const string code = @"
using Azure;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task<Response> Get1Async(string s, CancellationToken cancellationToken = default)
        {
            return null;
        }

        public virtual Response Get1(string s, CancellationToken cancellationToken = default)
        {
            return null;
        }

        public virtual Task<Response> Get1Async(string s, RequestContext context)
        {
            return null;
        }

        public virtual Response Get1(string s, RequestContext context)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0002NotProducedForMethodsWithRequestContext()
        {
            const string code = @"
using Azure;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task<Response> Get2Async(RequestContext context)
        {
            return null;
        }

        public virtual Response Get2(RequestContext context)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0002NotProducedIfThereIsAnOverloadWithCancellationToken()
        {
            const string code = @"
using Azure;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task<Response> GetAsync(string s)
        {
            return null;
        }

        public virtual Response Get(string s)
        {
            return null;
        }

        public virtual Task<Response> GetAsync(string s, CancellationToken cancellationToken)
        {
            return null;
        }

        public virtual Response Get(string s, CancellationToken cancellationToken)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0002NotProducedForGetSubClientMethods()
        {
            const string code = @"
namespace RandomNamespace
{
    public class SomeClient
    {
        public class Operation {}
        public virtual Operation GetOperationClient(string apiVersion = ""1.0.0"")
        {
            return new Operation();
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .RunAsync();
        }
    }
}
