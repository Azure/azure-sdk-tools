// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.ClientMethodsAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0015SystemClientModelTests
    {
        [Theory]
        [InlineData("public System.ClientModel.ClientResult ClientMethodAsync() { return default; }")]
        [InlineData("public Task<System.ClientModel.ClientResult> ClientMethodAsync() { return default; }")]
        [InlineData("public System.ClientModel.ClientResult<int> ClientMethodAsync() { return default; }")]
        [InlineData("public Task<System.ClientModel.ClientResult<int>> ClientMethodAsync() { return default; }")]
        [InlineData("public System.ClientModel.CollectionResult<int> ClientMethodAsync() { return default; }")]
        [InlineData("public Task<System.ClientModel.CollectionResult<int>> ClientMethodAsync() { return default; }")]
        [InlineData("public System.ClientModel.AsyncCollectionResult<int> ClientMethodAsync() { return default; }")]
        public async Task AZC0015NotProducedForSystemClientModelReturnTypes(string usage)
        {
            string code = $@"
using System;
using System.Threading.Tasks;
using Azure;

namespace System.ClientModel
{{
    public class ClientResult {{ }}
    public class ClientResult<T> {{ }}
    public class CollectionResult<T> {{ }}
    public class AsyncCollectionResult<T> {{ }}
}}

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