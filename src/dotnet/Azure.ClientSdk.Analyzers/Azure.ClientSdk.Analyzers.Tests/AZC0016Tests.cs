// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.OperationConstructorAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0016Tests
    {
        [Fact]
        public async Task AZC0016ProducedForOperationTypesWithoutProtectedCtor()
        {
            const string code = @"
namespace RandomNamespace
{
    public class Operation { }

    public class [|CertificateOperation|]: Operation
    {
        internal CertificateOperation() {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, "AZC0016");
        }

        [Fact]
        public async Task AZC0016ProducedForOperationTypesWithoutProtectedCtorGenericBaseType()
        {
            const string code = @"
namespace RandomNamespace
{
    public class Operation<T> { }

    public class [|CertificateOperation|]: Operation<int>
    {
        internal CertificateOperation() {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, "AZC0016");
        }

        [Fact]
        public async Task AZC0016NotProducedForOperationTypesWithProtectedCtor()
        {
            const string code = @"
namespace RandomNamespace
{
    public class Operation<T> { }

    public class CertificateOperation: Operation<int>
    {
        internal CertificateOperation(string s) {}
        protected CertificateOperation() {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }
    }
}