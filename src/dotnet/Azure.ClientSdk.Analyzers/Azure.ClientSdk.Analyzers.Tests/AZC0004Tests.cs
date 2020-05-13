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
        public virtual Task [|GetAsync|](CancellationToken cancellationToken = default)
        {
            return null;
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, "AZC0004");
        }

        [Fact]
        public async Task AZC0004NotProducedForMethodsWithoutSyncAlternative()
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
            await Verifier.VerifyAnalyzerAsync(code);
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
        
        public virtual Task [|GetAsync|]<T>(CancellationToken cancellationToken = default)
        {
            return null;
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, "AZC0004");
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
            await Verifier.VerifyAnalyzerAsync(code);
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
        
        public virtual Task [|GetAsync|]<T>(T item, CancellationToken cancellationToken = default)
        {
            return null;
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, "AZC0004");
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
            await Verifier.VerifyAnalyzerAsync(code);
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
        public virtual Task [|GetAsync|](int sameNameDifferentType, CancellationToken cancellationToken = default)
        {
            return null;
        }

        public virtual void Get(string sameNameDifferentType, CancellationToken cancellationToken = default)
        {
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, "AZC0004");
        }
    }
}