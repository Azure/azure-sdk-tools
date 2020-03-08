// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Azure.ClientSdk.Analyzers.Tests 
{
    public static class AzureAnalyzerVerifier<TAnalyzer> where TAnalyzer : DiagnosticAnalyzer, new()
    {
        public static AzureAnalyzerTest<TAnalyzer> CreateAnalyzer(string source, string expectedDescriptor = default, LanguageVersion languageVersion = LanguageVersion.Latest)
            => new AzureAnalyzerTest<TAnalyzer> (languageVersion)
            {
                TestCode = source,
                DescriptorName = expectedDescriptor,
                TestBehaviors = TestBehaviors.SkipGeneratedCodeCheck
            };

        public static Task VerifyAnalyzerAsync(string source, string expectedDescriptor = default, LanguageVersion languageVersion = LanguageVersion.Latest) 
            => CreateAnalyzer(source, expectedDescriptor, languageVersion).RunAsync(CancellationToken.None);

        public static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] diagnostics) {
            var test = new AzureAnalyzerTest<TAnalyzer> 
            {
                TestCode = source,
                TestBehaviors = TestBehaviors.SkipGeneratedCodeCheck
            };

            test.ExpectedDiagnostics.AddRange(diagnostics);
            return test.RunAsync(CancellationToken.None);
        }

        public static DiagnosticResult Diagnostic(string expectedDescriptor) => AnalyzerVerifier<TAnalyzer>.Diagnostic(expectedDescriptor);
    }
}