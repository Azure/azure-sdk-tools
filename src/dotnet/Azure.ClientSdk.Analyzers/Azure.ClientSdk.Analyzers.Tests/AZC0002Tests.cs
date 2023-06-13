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
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task {|AZC0002:GetAsync|}()
        {
            return null;
        }

        public virtual void {|AZC0002:Get|}()
        {
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0002ProducedForMethodsWithWrongNameCancellationToken()
        {
            const string code = @"
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task {|AZC0002:GetAsync|}(CancellationToken cancellation = default)
        {
            return null;
        }

        public virtual void {|AZC0002:Get|}(CancellationToken cancellation = default)
        {
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0002ProducedForMethodsWithWrongNameRequestContext()
        {
            const string code = @"
using Azure;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task {|AZC0002:GetAsync|}(RequestContext cancellation = default)
        {
            return null;
        }

        public virtual void {|AZC0002:Get|}(RequestContext cancellation = default)
        {
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0002ProducedForMethodsWithNonOptionalCancellationToken()
        {
            const string code = @"
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task {|AZC0002:GetAsync|}(CancellationToken cancellationToken)
        {
            return null;
        }

        public virtual void {|AZC0002:Get|}(CancellationToken cancellationToken)
        {
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
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
        public virtual Task {|AZC0002:GetAsync|}(RequestContext context = default, string text = default)
        {
            return null;
        }

        public virtual void {|AZC0002:Get|}(RequestContext context = default, string text = default)
        {
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
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
        public virtual Task {|AZC0002:GetAsync|}(CancellationToken cancellationToken = default, string text = default)
        {
            return null;
        }

        public virtual void {|AZC0002:Get|}(CancellationToken cancellationToken = default, string text = default)
        {
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0002NotProducedForMethodsWithCancellationToken()
        {
            const string code = @"
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task GetAsync(CancellationToken cancellationToken = default)
        {
            return null;
        }

        public virtual void Get(CancellationToken cancellationToken = default)
        {
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
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
        public virtual Task Get1Async(string s, CancellationToken cancellationToken = default)
        {
            return null;
        }

        public virtual void Get1(string s, CancellationToken cancellationToken = default)
        {
        }

        public virtual Task Get1Async(string s, RequestContext context)
        {
            return null;
        }

        public virtual void Get1(string s, RequestContext context)
        {
        }

        public virtual Task Get2Async(CancellationToken cancellationToken = default)
        {
            return null;
        }

        public virtual void Get2(CancellationToken cancellationToken = default)
        {
        }

        public virtual Task Get2Async(RequestContext context)
        {
            return null;
        }

        public virtual void Get2(RequestContext context)
        {
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .WithDisabledDiagnostics("AZC0018")
                .WithDisabledDiagnostics("AD0001")
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
        public virtual Task GetAsync(RequestContext context = default)
        {
            return null;
        }

        public virtual void Get(RequestContext context = default)
        {
        }

        public virtual Task Get2Async(RequestContext context)
        {
            return null;
        }

        public virtual void Get2(RequestContext context)
        {
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .WithDisabledDiagnostics("AZC0018")
                .RunAsync();
        }
    }
}
