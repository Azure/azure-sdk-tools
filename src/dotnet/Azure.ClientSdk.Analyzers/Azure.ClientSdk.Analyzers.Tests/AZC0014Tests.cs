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
        [InlineData("public class [|Class|]: System.IProgress<JsonElement> { public void Report (JsonElement [|value|]) {} }")]
        [InlineData("public void Report(JsonElement [|value|]) {}")]
        [InlineData("public JsonElement [|Report|]() { return default; }")]
        [InlineData("public IEnumerable<JsonElement> [|Report|]() { return default; }")]
        [InlineData("public JsonElement [|Report|] { get; }")]
        [InlineData("public JsonElement [|Report|];")]
        [InlineData("public event EventHandler<JToken> [|Report|];")]
        [InlineData("protected JToken [|Report|];")]
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
            await Verifier.VerifyAnalyzerAsync(code, "AZC0014");
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