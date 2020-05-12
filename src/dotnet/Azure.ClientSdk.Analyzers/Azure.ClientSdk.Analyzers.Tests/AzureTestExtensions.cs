// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public static class AzureTestExtensions
    {
        public static AzureAnalyzerTest<TAnalyzer> WithSources<TAnalyzer>(this AzureAnalyzerTest<TAnalyzer> test, params string[] sources)
            where TAnalyzer : DiagnosticAnalyzer, new()
        {
            foreach (var source in sources)
            {
                test.TestState.Sources.Add(source);
            }
            return test;
        }

        public static AzureAnalyzerTest<TAnalyzer> WithDisabledDiagnostics<TAnalyzer>(this AzureAnalyzerTest<TAnalyzer> test, params string[] diagnostics)
            where TAnalyzer : DiagnosticAnalyzer, new()
        {
            test.DisabledDiagnostics.AddRange(diagnostics);
            return test;
        }

        public static AzureRefactoringTest<TRefactoring> WithSources<TRefactoring>(this AzureRefactoringTest<TRefactoring> test, params string[] sources)
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