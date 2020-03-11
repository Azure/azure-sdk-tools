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
        public virtual Task [|GetAsync|]()
        {
            return null;
        }

        public virtual void [|Get|]()
        {
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, "AZC0002");
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
        public virtual Task [|GetAsync|](CancellationToken cancellationToken)
        {
            return null;
        }

        public virtual void [|Get|](CancellationToken cancellationToken)
        {
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, "AZC0002");
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
        public virtual Task [|GetAsync|](CancellationToken cancellation = default)
        {
            return null;
        }

        public virtual void [|Get|](CancellationToken cancellation = default)
        {
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, "AZC0002");
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
            await Verifier.VerifyAnalyzerAsync(code);
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
        public virtual Task [|GetAsync|](string s)
        {
            return null;
        }

        public virtual void [|Get|](string s)
        {
        }

        public virtual Task [|GetAsync|](CancellationToken cancellationToken)
        {
            return null;
        }

        public virtual void [|Get|](CancellationToken cancellationToken)
        {
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, "AZC0002");
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
            await Verifier.VerifyAnalyzerAsync(code, "AZC0002");
        }
    }
}