// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.BannedTypesAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0020Tests
    {
        [Theory]
        [InlineData("public class {|AZC0020:Class|}: System.IProgress<MutableJsonDocument> { public void Report (JsonElement {|AZC0020:value|}) {} }")]
        [InlineData("public void Report(MutableJsonDocument {|AZC0020:value|}) {}")]
        [InlineData("public MutableJsonDocument {|AZC0020:Report|}() { return default; }")]
        [InlineData("public IEnumerable<MutableJsonDocument> {|AZC0020:Report|}() { return default; }")]
        [InlineData("public MutableJsonDocument {|AZC0020:Report|} { get; }")]
        [InlineData("public MutableJsonDocument {|AZC0020:Report|};")]
        [InlineData("public event EventHandler<MutableJsonDocument> {|AZC0020:Report|};")]
        [InlineData("protected MutableJsonDocument {|AZC0020:Report|};")]
        public async Task AZC0020ProducedForMutableJsonTypeUsageInPublicApi(string usage)
        {
            string code = $@"
using System;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using Azure.Core.Json;

namespace RandomNamespace
{{
    public class SomeClient
    {{
        {usage}       
    }}
}}";
            await Verifier.VerifyAnalyzerAsync(code);
        }


        [Theory]
        [InlineData("internal class Class: System.IProgress<JsonElement> { public void Report (JsonElement value) {} }")]
        [InlineData("public void Report(string value) {}")]
        public async Task AZC0020NotProducedForNonPublicApisOrAllowedTypes(string usage)
        {
            string code = $@"
using System;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using Azure.Core.Json;

namespace RandomNamespace
{{
    public class SomeClient
    {{
        {usage}       
    }}
}}";
            await Verifier.VerifyAnalyzerAsync(code);
        }
    }
}
