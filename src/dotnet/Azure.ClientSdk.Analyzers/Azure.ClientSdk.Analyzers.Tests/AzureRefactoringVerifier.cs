// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Testing;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public static class AzureRefactoringVerifier<TRefactoring> where TRefactoring : CodeRefactoringProvider, new()
    {
        public static AzureRefactoringTest<TRefactoring> CreateRefactoring(string source, string fixedCode)
            => new AzureRefactoringTest<TRefactoring> ()
            {
                TestCode = source,
                FixedCode = fixedCode,
                TestBehaviors = TestBehaviors.SkipGeneratedCodeCheck
            };
    }
}