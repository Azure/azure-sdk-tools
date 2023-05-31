// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.ClientMethodsAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0004Tests
    {
        [Fact]
        public async Task AZC0004ProducedForMethodsWithoutSyncAlternative()
        {
            const string code = @"
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task {|AZC0004:GetAsync|}(CancellationToken cancellationToken = default)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0004ProducedForMethodsWithoutAsyncAlternative()
        {
            const string code = @"
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task {|AZC0004:Get|}(CancellationToken cancellationToken = default)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0004NotProducedForMethodsWithCancellationToken()
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
        public virtual Task Get(CancellationToken cancellationToken = default)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0004NotProducedForMethodsWithOptionalRequestContext()
        {
            const string code = @"
using Azure;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task GetAsync(RequestContext context = null)
        {
            return null;
        }
        public virtual Task Get(RequestContext context = null)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .WithDisabledDiagnostics("AZC0018")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0004NotProducedForMethodsWithRequiredRequestContext()
        {
            const string code = @"
using Azure;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task<Response> GetAsync(RequestContext context)
        {
            return null;
        }
        public virtual Response Get(RequestContext context)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0004ProducedForMethodsNotMatch()
        {
            const string code = @"
using Azure;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task {|AZC0004:GetAsync|}(string a, CancellationToken cancellationToken = default)
        {
            return null;
        }
        public virtual Task {|AZC0004:Get|}(string a, RequestContext context)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0004ProducedForMethodsWithNotMatchedRequestContext()
        {
            const string code = @"
using Azure;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task<Response> {|AZC0004:GetAsync|}(RequestContext context = null)
        {
            return null;
        }
        public virtual Response {|AZC0004:Get|}(RequestContext context)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .WithDisabledDiagnostics("AZC0018")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0004ProducedForGenericMethodsWithSyncAlternative()
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
        
        public virtual Task {|AZC0004:GetAsync|}<T>(CancellationToken cancellationToken = default)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0004NotProducedForGenericMethodsWithSyncAlternative()
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
        
        public virtual Task GetAsync<T>(CancellationToken cancellationToken = default)
        {
            return null;
        }

        public virtual Task Get<T>(CancellationToken cancellationToken = default)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0004ProducedForGenericMethodsTakingGenericArgWithoutSyncAlternative()
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
        
        public virtual Task {|AZC0004:GetAsync|}<T>(T item, CancellationToken cancellationToken = default)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0004NotProducedForGenericMethodsTakingGenericArgWithSyncAlternative()
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
        
        public virtual Task GetAsync<T>(T item, CancellationToken cancellationToken = default)
        {
            return null;
        }

        public virtual Task Get<T>(T item, CancellationToken cancellationToken = default)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0004NProducedForMethodsWithoutArgMatchedSyncAlternative()
        {
            const string code = @"
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task {|AZC0004:GetAsync|}(int sameNameDifferentType, CancellationToken cancellationToken = default)
        {
            return null;
        }

        public virtual void {|AZC0004:Get|}(string sameNameDifferentType, CancellationToken cancellationToken = default)
        {
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0004NotProducedForGenericMethodsTakingGenericExpressionArgWithSyncAlternative()
        {
            const string code = @"
using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task QueryAsync<T>(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
        {
            return null;
        }

        public virtual void Query<T>(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
        {
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0004ProducedForGenericMethodsTakingGenericExpressionArgWithoutSyncAlternative()
        {
            const string code = @"
using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task {|AZC0004:QueryAsync|}<T>(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
        {
            return null;
        }

        public virtual void {|AZC0004:Query|}<T>(Expression<Func<T, string, bool>> filter, CancellationToken cancellationToken = default)
        {
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0004NotProducedForArrayTypesWithSyncAlternative()
        {
            const string code = @"
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task AppendAsync(
            byte[] arr,
            CancellationToken cancellationToken = default)
        {
            return null;
        }


        public virtual void Append(
            byte[] arr,
            CancellationToken cancellationToken = default)
        {
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0004ProducedForArrayTypesWithoutSyncAlternative()
        {
            const string code = @"
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task {|AZC0004:AppendAsync|}(
            byte[] arr,
            CancellationToken cancellationToken = default)
        {
            return null;
        }


        public virtual void {|AZC0004:Append|}(
            string[] arr,
            CancellationToken cancellationToken = default)
        {
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0004ProducedForMethodsWithoutSyncAlternativeWithMatchingArgNames()
        {
            const string code = @"
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task {|AZC0004:GetAsync|}(int foo, CancellationToken cancellationToken = default)
        {
            return null;
        }

        public virtual void {|AZC0004:Get|}(int differentName, CancellationToken cancellationToken = default)
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
