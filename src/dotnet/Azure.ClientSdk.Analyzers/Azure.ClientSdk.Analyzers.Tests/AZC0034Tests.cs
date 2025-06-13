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
        [InlineData("Azure.Data", "BlobClient", true)]
        [InlineData("Azure.MyService", "BlobClient", true)]
        [InlineData("Azure.MyService", "PageBlobClient", true)]
        [InlineData("MyCompany.Data", "BlobClient", false)]
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

        [Fact]
        public async Task AZC0034IncludesPackageNameInMessage()
        {
            var code = @"
namespace Azure.Test
{
    public class BlobClient { }
}";

            var expected = Verifier.Diagnostic("AZC0034").WithSpan(4, 18, 4, 28).WithArguments("BlobClient", "Azure.Storage.Blobs.BlobClient (from Azure.Storage.Blobs)");
            await Verifier.VerifyAnalyzerAsync(code, expected);
        }
    }
}