// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.BannedTypesAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0020Tests
    {
        [Fact]
        public async Task AZC0020ProducedForMutableJsonDocumentUsage()
        {
            string code = @"
using Azure.Core.Json;

namespace LibraryNamespace
{
    public class Model
    {
        MutableJsonDocument {|AZC0020:_document|};
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0020ProducedForMutableJsonElementUsage()
        {
            string code = @"
using Azure.Core.Json;

namespace LibraryNamespace
{
    public class Model
    {
        MutableJsonElement {|AZC0020:_element|};
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0020NotProducedForAllowedTypeUsage()
        {
            string code = @"
using System.Text.Json;

namespace LibraryNamespace
{
    public class Model
    {
        JsonElement _element;
    }
}";
            await Verifier.VerifyAnalyzerAsync(code);
        }
    }
}
