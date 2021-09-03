// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public static class AzureRefactoringVerifier<TRefactoring> where TRefactoring : CodeRefactoringProvider, new()
    {
        private static readonly ReferenceAssemblies DefaultReferenceAssemblies =
            ReferenceAssemblies.Default.AddPackages(ImmutableArray.Create(
                new PackageIdentity("Microsoft.Bcl.AsyncInterfaces", "1.1.0"),
                new PackageIdentity("System.Threading.Tasks.Extensions", "4.5.3")));

        public static CSharpCodeRefactoringTest<TRefactoring, XUnitVerifier> CreateRefactoring(string source, string fixedCode)
            => new CSharpCodeRefactoringTest<TRefactoring, XUnitVerifier>()
            {
                ReferenceAssemblies = DefaultReferenceAssemblies,
                TestCode = source,
                FixedCode = fixedCode,
                TestBehaviors = TestBehaviors.SkipGeneratedCodeCheck
            };
    }
}