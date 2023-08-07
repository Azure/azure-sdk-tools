// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.BannedTypesAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0020Tests
    {
        private List<(string fileName, string source)> _sharedSourceFiles;

        public AZC0020Tests()
        {
            _sharedSourceFiles = new List<(string fileName, string source)>() {

            ("MutableJsonDocument.cs", @"
                namespace Azure.Core.Json
                {
                    internal sealed partial class MutableJsonDocument
                    {
                    }
                }
                "),

            ("MutableJsonElement.cs", @"
                namespace Azure.Core.Json
                {
                    internal sealed partial class MutableJsonElement
                    {
                    }
                }
                ")
            };
        }

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
            await Verifier.VerifyAnalyzerAsync(code, _sharedSourceFiles);
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
            await Verifier.VerifyAnalyzerAsync(code, _sharedSourceFiles);
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
            await Verifier.VerifyAnalyzerAsync(code, _sharedSourceFiles);
        }
    }
}
