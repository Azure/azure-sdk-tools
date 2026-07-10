// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.OperationConstructorAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0005Tests
    {
        [Fact]
        public async Task AZC0005ProducedForOperationTypesWithoutParameterlessCtor()
        {
            const string code = @"
namespace Azure
{
    public class Operation {}
}

namespace RandomNamespace
{
    public class {|AZC0005:SomeOperation|} : Azure.Operation
    {
        public SomeOperation(string connectionString) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0005NotProducedForOperationTypesWithProtectedParameterlessCtor()
        {
            const string code = @"
namespace Azure
{
    public class Operation {}
}

namespace RandomNamespace
{
    public class SomeOperation : Azure.Operation
    {
        protected SomeOperation() {}
        public SomeOperation(string connectionString) {}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }
    }
}
