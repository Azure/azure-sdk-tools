// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.DuplicateTypeNameAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0034Tests
    {
        [Theory]
        [InlineData("Azure.Data", "String", true)]
        [InlineData("Azure.MyService", "BlobClient", true)]
        [InlineData("Azure.MyService", "CosmosClient", true)]
        [InlineData("MyCompany.Data", "String", false)]
        public async Task AZC0034ProducedForReservedTypeNames(string namespaceName, string typeName, bool shouldReport)
        {
            var code = shouldReport 
                ? $@"
namespace {namespaceName}
{{
    public class {{|AZC0034:{typeName}|}} {{ }}
}}"
                : $@"
namespace {namespaceName}
{{
    public class {typeName} {{ }}
}}";

            await Verifier.VerifyAnalyzerAsync(code);
        }
    }
}