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
                    internal partial struct MutableJsonElement
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
using System;
using Azure.Core.Json;

namespace LibraryNamespace
{
    public class Model
    {
        private MutableJsonDocument {|AZC0020:_document|};
        internal MutableJsonDocument {|AZC0020:Document|} => {|AZC0020:_document|};
        internal event EventHandler<MutableJsonDocument> {|AZC0020:_docEvent|};

        internal MutableJsonDocument {|AZC0020:GetDocument|}(MutableJsonDocument {|AZC0020:value|})
        {
            {|AZC0020:MutableJsonDocument mdoc = new MutableJsonDocument();|}
            return mdoc;
        }
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
        private MutableJsonElement {|AZC0020:_element|};
        internal MutableJsonElement {|AZC0020:Element|} => {|AZC0020:_element|};

        internal MutableJsonElement {|AZC0020:GetDocument|}(MutableJsonElement {|AZC0020:value|})
        {
            {|AZC0020:MutableJsonElement element = new MutableJsonElement();|}
            return element;
        }
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

        [Fact]
        public async Task AZC0020NotProducedForTypeWithBannedNameInAllowedNamespace()
        {
            string code = @"
namespace LibraryNamespace
{
    public class MutableJsonDocument
    {
    }
    public class Model
    {
        MutableJsonDocument _document;
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, _sharedSourceFiles);
        }
    }
}
