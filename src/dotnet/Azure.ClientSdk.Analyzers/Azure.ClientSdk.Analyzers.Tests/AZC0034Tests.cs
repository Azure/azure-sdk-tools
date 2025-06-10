// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.DuplicateTypeNameAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0034Tests
    {
        [Fact]
        public async Task AZC0034ProducedForPlatformTypeNameConflicts()
        {
            const string code = @"
namespace Azure.Data
{
    public class {|AZC0034:String|} { }
}";

            await Verifier.VerifyAnalyzerAsync(code);
        }



        [Fact]
        public async Task AZC0034NotProducedForNonAzureNamespaces()
        {
            const string code = @"
namespace MyCompany.Data
{
    public class String { }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0034ProducedForAzureSDKTypeConflicts()
        {
            const string code = @"
namespace Azure.MyService
{
    public class {|AZC0034:BlobClient|} { }
    public class {|AZC0034:CosmosClient|} { }
}";

            await Verifier.VerifyAnalyzerAsync(code);
        }


    }
}