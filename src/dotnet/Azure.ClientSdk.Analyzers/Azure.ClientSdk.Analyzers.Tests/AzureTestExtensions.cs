// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public static class AzureTestExtensions
    {
        public static CSharpAnalyzerTest<TAnalyzer, XUnitVerifier> WithSources<TAnalyzer>(this CSharpAnalyzerTest<TAnalyzer, XUnitVerifier> test, params string[] sources)
            where TAnalyzer : DiagnosticAnalyzer, new()
        {
            foreach (var source in sources)
            {
                test.TestState.Sources.Add(source);
            }
            return test;
        }

        public static CSharpAnalyzerTest<TAnalyzer, XUnitVerifier> WithDisabledDiagnostics<TAnalyzer>(this CSharpAnalyzerTest<TAnalyzer, XUnitVerifier> test, params string[] diagnostics)
            where TAnalyzer : DiagnosticAnalyzer, new()
        {
            test.DisabledDiagnostics.AddRange(diagnostics);
            return test;
        }

        public static CSharpCodeRefactoringTest<TRefactoring, XUnitVerifier> WithSources<TRefactoring>(this CSharpCodeRefactoringTest<TRefactoring, XUnitVerifier> test, params string[] sources)
            where TRefactoring : CodeRefactoringProvider, new()
        {
            foreach (var source in sources)
            {
                test.TestState.Sources.Add(source);
                test.FixedState.Sources.Add(source);
            }
            return test;
        }

    }
}