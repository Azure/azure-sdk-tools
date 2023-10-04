// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.BannedTypesAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC1200Tests
    {
        private List<(string fileName, string source)> _sharedSourceFiles;

        public AZC1200Tests()
        {
            _sharedSourceFiles = new List<(string fileName, string source)>() {

            ("IFoo.cs", @"
                namespace Azure.Foo
                {
                    internal interface IFoo
                    {
                    }
                }
                "),
            };
        }

        [Fact]
        public async Task AZC0020ProducedForMutableJsonDocumentUsage()
        {
            string code = @"
using System;
using Azure.Foo;

namespace LibraryNamespace
{
    public class Model : IFoo
    {    
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, _sharedSourceFiles);
        }

    }
}
