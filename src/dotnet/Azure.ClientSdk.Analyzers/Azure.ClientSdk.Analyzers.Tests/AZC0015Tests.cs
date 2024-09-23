// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.ClientMethodsAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0015Tests
    {
        [Theory]
        [InlineData("public Task<AsyncPageable<int>> {|AZC0015:ClientMethodAsync|}() { return default; }")]
        [InlineData("public Task<Pageable<int>> {|AZC0015:ClientMethodAsync|}() { return default; }")]
        [InlineData("public int {|AZC0015:ClientMethodAsync|}() { return default; }")]
        [InlineData("public int[] {|AZC0015:ClientMethodAsync|}() { return default; }")]
        [InlineData("public Task<int[]> {|AZC0015:ClientMethodAsync|}() { return default; }")]
        [InlineData("public ValueTask<Response<int>> {|AZC0015:ClientMethodAsync|}() { return default; }")]
        [InlineData("public string {|AZC0015:ClientMethodAsync|}() { return default; }")]
        public async Task AZC0015ProducedForInvalidClientMethodReturnTypes(string usage)
        {
            string code = $@"
using System;
using System.Threading.Tasks;
using Azure;

namespace RandomNamespace
{{
    public class SomeClient
    {{
        {usage}       
    }}
}}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0002", "AZC0003", "AZC0004")
                .RunAsync();
        }

        [Theory]
        [InlineData("public Task<Operation<int>> ClientMethodAsync() { return default; }")]
        [InlineData("public Operation<int> ClientMethodAsync() { return default; }")]
        [InlineData("public Pageable<int> ClientMethodAsync() { return default; }")]
        [InlineData("public AsyncPageable<int> ClientMethodAsync() { return default; }")]
        [InlineData("public Response<int> ClientMethodAsync() { return default; }")]
        [InlineData("public Response<int[]> ClientMethodAsync() { return default; }")]
        [InlineData("public Task<Response<int[]>> ClientMethodAsync() { return default; }")]
        [InlineData("public NullableResponse<int[]> ClientMethodAsync() { return default; }")]
        [InlineData("public Task<NullableResponse<int[]>> ClientMethodAsync() { return default; }")]
        [InlineData("public Response ClientMethodAsync() { return default; }")]
        [InlineData("public SomeClient ClientMethod() { return default; }")]
        public async Task AZC0015NotProducedForValidReturnTypes(string usage)
        {
            string code = $@"
using System;
using System.Threading.Tasks;
using Azure;

namespace RandomNamespace
{{
    public class SomeClient
    {{
        {usage}       
    }}
}}";
            await Verifier.CreateAnalyzer(code)
                .WithDisabledDiagnostics("AZC0002", "AZC0003", "AZC0004")
                .RunAsync();
        }
    }
}
