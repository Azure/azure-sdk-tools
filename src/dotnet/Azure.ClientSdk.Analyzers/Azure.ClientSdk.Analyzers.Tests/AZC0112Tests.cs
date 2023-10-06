// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.InternalsVisibleToAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0112Tests
    {
        private List<(string fileName, string source)> _sharedSourceFiles;

        public AZC0112Tests()
        { }

        [Fact]
        public async Task AZC0020ProducedForMutableJsonDocumentUsage()
        {
            string code = @"
using System;
using TestReferenceWithInternalsVisibleTo;
using System.Reflection;

namespace LibraryNamespace
{
    public class {|AZC0112:Model|} : IInternalInterface
    {    
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, additionalReferences: new[] { typeof(TestReferenceWithInternalsVisibleTo.PublicClass) });
        }

    }
}
