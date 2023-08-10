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
        public async Task AZC0002ProducedForMethodsWithoutCancellationToken()
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
        public async Task AZC0002ProducedForMethodsWithWrongNameParameter()
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
        public async Task AZC0002DoesntFireIfThereIsAnOverloadWithCancellationToken()
        {
            const string code = @"
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task GetAsync(string s)
        {
            return null;
        }

        public virtual void Get(string s)
        {
        }

        public virtual Task GetAsync(string s, CancellationToken cancellationToken)
        {
            return null;
        }

        public virtual void Get(string s, CancellationToken cancellationToken)
        {
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0002DoesntFireIfThereIsAnOverloadWithRequestContext()
        {
            const string code = @"
using Azure;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task GetAsync(string s)
        {
            return null;
        }

        public virtual void Get(string s)
        {
        }

        public virtual Task GetAsync(string s, RequestContext context = default)
        {
            return null;
        }

        public virtual void Get(string s, RequestContext context = default)
        {
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0002ProducedWhenCancellationTokenOverloadsDontMatch()
        {
            const string code = @"
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task {|AZC0002:GetAsync|}(string s)
        {
            return null;
        }

        public virtual void {|AZC0002:Get|}(string s)
        {
        }

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
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .RunAsync();
        }
    }
}
