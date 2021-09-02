// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.ClientMethodsAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0003Tests
    {
        [Fact]
        public async Task AZC0003ProducedForNonVirtualMethods()
        {
            const string code = @"
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public Task {|AZC0003:GetAsync|](CancellationToken cancellationToken = default)
        {
            return null;
        }

        public void {|AZC0003:Get|](CancellationToken cancellationToken = default)
        {
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0003SkippedForPrivateMethods()
        {
            const string code = @"
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        private Task GetAsync(CancellationToken cancellationToken = default)
        {
            return null;
        }

        private void Get(CancellationToken cancellationToken = default)
        {
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0015")
                .RunAsync();
        }

        [Fact]
        public async Task AZC0003SkippedForOverrides()
        {
            const string code = @"
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class C
    {
        public virtual Task GetAsync(CancellationToken cancellationToken = default)
        {
            return null;
        }
    }

    public class SomeClient: C
    {
        public override Task GetAsync(CancellationToken cancellationToken = default)
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
    }
}