// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.BannedAssembliesAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0014Tests
    {
        [Theory]
        [InlineData("public class {|AZC0014:Class|}: System.IProgress<JsonElement> { public void Report (JsonElement {|AZC0014:value|}) {} }")]
        [InlineData("public void Report(JsonElement {|AZC0014:value|}) {}")]
        [InlineData("public JsonElement {|AZC0014:Report|}() { return default; }")]
        [InlineData("public IEnumerable<JsonElement> {|AZC0014:Report|}() { return default; }")]
        [InlineData("public JsonElement {|AZC0014:Report|} { get; }")]
        [InlineData("public JsonElement {|AZC0014:Report|};")]
        [InlineData("public event EventHandler<JToken> {|AZC0014:Report|};")]
        [InlineData("protected JToken {|AZC0014:Report|};")]
        public async Task AZC0014ProducedForJsonTypeUsageInPublicApi(string usage)
        {
            string code = $@"
using System;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

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
        public async Task AZC0014NotProducedForNonPublicApisOrAllowedTypes(string usage)
        {
            string code = $@"
using System;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

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