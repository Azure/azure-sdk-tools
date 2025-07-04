// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.DuplicateTypeNameAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0034Tests
    {
        [Theory]
        [InlineData("Azure.Data", "BlobClient", true)]
        [InlineData("Azure.MyService", "BlobClient", true)]
        [InlineData("Azure.MyService", "PageBlobClient", true)]
        [InlineData("MyCompany.Data", "BlobClient", false)]
        public async Task AZC0034ProducedForReservedTypeNames(string namespaceName, string typeName, bool shouldReport)
        {
            var code = shouldReport 
                ? $@"
namespace {namespaceName}
{{
    public class {{|AZC0034:{typeName}|}} {{ }}
}}"
                : $@"
namespace {namespaceName}
{{
    public class {typeName} {{ }}
}}";

            await Verifier.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task AZC0034IncludesPackageNameInMessage()
        {
            var code = @"
namespace Azure.Test
{
    public class BlobClient { }
}";

            var expected = Verifier.Diagnostic("AZC0034").WithSpan(4, 18, 4, 28).WithArguments("BlobClient", "Azure.Storage.Blobs.BlobClient (from Azure.Storage.Blobs)", "Consider renaming to 'TestBlobClient' or 'TestServiceClient' to avoid confusion.");
            await Verifier.VerifyAnalyzerAsync(code, expected);
        }

        [Fact]
        public async Task AZC0034NotProducedForSameTypeInSameAssembly()
        {
            // This test reproduces the false positive issue where a type defined in Azure.Core
            // is flagged as conflicting with itself in the reserved types list
            var test = Verifier.CreateAnalyzer("");
            test.TestState.Sources.Add(@"[assembly: System.Reflection.AssemblyTitle(""Azure.Core"")]

namespace Azure
{
    public abstract class Operation { }
    public abstract class Response { }
}");

            // Set the assembly name to match what's expected in the reserved types file
            test.SolutionTransforms.Add((solution, projectId) =>
            {
                var project = solution.GetProject(projectId);
                return solution.WithProjectAssemblyName(projectId, "Azure.Core");
            });

            // Should not produce any diagnostics since these are the exact same types as in the reserved list
            await test.RunAsync();
        }

        [Fact]
        public async Task AZC0034NotProducedForSameGenericTypeInSameAssembly()
        {
            // This test reproduces the false positive issue where a generic type defined in Azure.Core
            // is flagged as conflicting with the non-generic version in the reserved types list
            var test = Verifier.CreateAnalyzer("");
            test.TestState.Sources.Add(@"[assembly: System.Reflection.AssemblyTitle(""Azure.Core"")]

namespace Azure
{
    public abstract class Operation<T> { }
}");

            // Set the assembly name to match what's expected in the reserved types file
            test.SolutionTransforms.Add((solution, projectId) =>
            {
                var project = solution.GetProject(projectId);
                return solution.WithProjectAssemblyName(projectId, "Azure.Core");
            });

            // Should not produce any diagnostics since Operation<T> is defined in Azure.Core,
            // and even though it matches the "Operation" name, it should be skipped if it's from the same assembly
            await test.RunAsync();
        }

        [Fact]
        public async Task AZC0034NotProducedForNestedTypesInSameAssembly()
        {
            // This test covers the case for types like ArrayEnumerator and ObjectEnumerator
            // mentioned in the original issue
            var test = Verifier.CreateAnalyzer("");
            test.TestState.Sources.Add(@"[assembly: System.Reflection.AssemblyTitle(""Azure.Core"")]

namespace Azure.Core
{
    public struct ArrayEnumerator { }
    public struct ObjectEnumerator { }
}");

            // Set the assembly name to match what's expected in the reserved types file
            test.SolutionTransforms.Add((solution, projectId) =>
            {
                var project = solution.GetProject(projectId);
                return solution.WithProjectAssemblyName(projectId, "Azure.Core");
            });

            // Should not produce any diagnostics since these are the same types defined in Azure.Core
            await test.RunAsync();
        }

        [Fact]
        public async Task AZC0034ProducedForGenericTypeConflictInDifferentAssembly()
        {
            // This test verifies that generic types can properly conflict with reserved generic types
            // when they are in different assemblies
            var code = @"
namespace Azure
{
    public abstract class Operation<T> { }
}";
            var expected = Verifier.Diagnostic("AZC0034").WithSpan(4, 27, 4, 36).WithArguments("Operation`1", "Azure.Operation`1 (from Azure.Core)", "Consider renaming to 'CustomOperation' or 'CustomProcess' to avoid confusion.");
            await Verifier.VerifyAnalyzerAsync(code, expected);
        }

        [Fact]
        public async Task AZC0034NotProducedForNestedTypes()
        {
            // This test verifies that nested types are not flagged by AZC0034
            // Even if they have names that would conflict with reserved types
            var code = @"
namespace Azure.Test
{
    public class ParentClass
    {
        public class BlobClient { }
        public struct Response { }
        public enum Operation { }
        
        public class Container
        {
            public class Enumerator { }
        }
    }
}";

            // No diagnostics should be produced for nested types
            await Verifier.VerifyAnalyzerAsync(code);
        }
    }
}
