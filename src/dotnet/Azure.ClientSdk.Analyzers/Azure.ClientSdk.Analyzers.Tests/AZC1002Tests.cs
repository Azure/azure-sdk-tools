// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC1002Tests
    {
        private readonly DiagnosticAnalyzerRunner _runner = new DiagnosticAnalyzerRunner(new WhitespaceNewLineAnalyzer());

        public static object[][] TestCases = new[]
        {
            @"
namespace RandomNamespace
{
/*MM*/    
}
",
            @"
namespace RandomNamespace
{
}/*MM*/    
",
            @"
namespace RandomNamespace
{/*MM*/    
}
",
            @"
namespace RandomNamespace/*MM*/     
{
}
",

        }.Select(s => new object[] { s }).ToArray();

        [Theory]
        [MemberData(nameof(TestCases))]
        public async Task AZC1002ProducedForWhitespaceInTheEndOfTheLine(string testCase)
        {
            var testSource = TestSource.Read(testCase);
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC1002", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }
    }
}