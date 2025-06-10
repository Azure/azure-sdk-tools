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
        public async Task AZC0034ProducedForCommonTypeNameConflicts()
        {
            const string code = @"
namespace Azure.Storage
{
    public class {|AZC0034:List|} { }
    public class {|AZC0034:Dictionary|} { }
    public class {|AZC0034:Task|} { }
}";

            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0034NotProducedForNonPublicTypes()
        {
            const string code = @"
namespace Azure.Data
{
    internal class String { }
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
        public async Task AZC0034NotProducedForUniqueTypeNames()
        {
            const string code = @"
namespace Azure.Data
{
    public class BlobClient { }
    public class TableEntity { }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0034NotProducedForAllowedNestedServiceVersion()
        {
            const string code = @"
namespace Azure.Data
{
    public class BlobClient
    {
        public enum ServiceVersion
        {
            V2020_02_10,
            V2021_04_10
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0034NotProducedForAllowedNestedEnumerator()
        {
            const string code = @"
namespace Azure.Data
{
    public class BlobList
    {
        public struct Enumerator
        {
            public bool MoveNext() => false;
            public object Current => null;
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0034ProducedForTopLevelServiceVersion()
        {
            const string code = @"
namespace Azure.Data
{
    public enum {|AZC0034:ServiceVersion|}
    {
        V2020_02_10
    }
}";

            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0034ProducedForTopLevelEnumerator()
        {
            const string code = @"
namespace Azure.Data
{
    public class {|AZC0034:Enumerator|}
    {
    }
}";

            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0034ProducedForExceptionTypeNameConflicts()
        {
            const string code = @"
namespace Azure.Data
{
    public class {|AZC0034:Exception|} { }
    public class {|AZC0034:ArgumentException|} { }
}";

            await Verifier.VerifyAnalyzerAsync(code);
        }
    }
}