// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.ClientModel;
using APIView;
using CSharpAPIParser.TreeToken;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CSharpAPIParserTests
{
    public class AnalyzerTests
    {
        private static readonly MetadataReference[] _references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => Path.GetDirectoryName(path) == Path.GetDirectoryName(typeof(object).Assembly.Location))
            .Concat(new[] { typeof(Azure.Response).Assembly.Location, typeof(ClientResult).Assembly.Location })
            .Distinct()
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray();

        [Theory]
        [InlineData("ClientResult", "Task<ClientResult>")]
        [InlineData("ClientResult<SessionLogEvent>", "Task<ClientResult<SessionLogEvent>>")]
        [InlineData("CollectionResult<ProjectAgentSession>", "AsyncCollectionResult<ProjectAgentSession>")]
        [InlineData("Response", "Task<Response>")]
        [InlineData("Response<SessionLogEvent>", "Task<Response<SessionLogEvent>>")]
        [InlineData("NullableResponse<SessionLogEvent>", "Task<NullableResponse<SessionLogEvent>>")]
        [InlineData("Operation", "Task<Operation>")]
        [InlineData("Operation<SessionLogEvent>", "Task<Operation<SessionLogEvent>>")]
        [InlineData("Pageable<ProjectAgentSession>", "AsyncPageable<ProjectAgentSession>")]
        public void ApprovedSyncAndAsyncReturnTypesHaveNoDiagnostics(string syncType, string asyncType)
        {
            var codeFile = BuildCodeFile($$"""
                public virtual {{syncType}} GetValue(CancellationToken cancellationToken = default) => throw new NotImplementedException();
                public virtual {{asyncType}} GetValueAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
                """);

            Assert.Empty(codeFile.Diagnostics);
        }

        [Fact]
        public void SessionClientMethodsHaveNoDiagnostics()
        {
            var codeFile = BuildCodeFile("""
                public virtual ClientResult<SessionLogEvent> GetSessionLogStream(string agentName, string agentVersion, string sessionId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
                public virtual Task<ClientResult<SessionLogEvent>> GetSessionLogStreamAsync(string agentName, string agentVersion, string sessionId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
                public virtual CollectionResult<ProjectAgentSession> GetSessions(string agentName, int? limit = null, AgentListOrder? order = null, string after = null, string before = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
                public virtual AsyncCollectionResult<ProjectAgentSession> GetSessionsAsync(string agentName, int? limit = null, AgentListOrder? order = null, string after = null, string before = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
                """);

            Assert.Empty(codeFile.Diagnostics);
        }

        [Theory]
        [InlineData("int")]
        [InlineData("Task")]
        [InlineData("Task<int>")]
        [InlineData("ValueTask<ClientResult>")]
        [InlineData("Task<Task<ClientResult>>")]
        [InlineData("Task<System.ClientModel.Primitives.CollectionResult>")]
        [InlineData("Task<CollectionResult<int>>")]
        [InlineData("Task<System.ClientModel.Primitives.AsyncCollectionResult>")]
        [InlineData("Task<AsyncCollectionResult<int>>")]
        [InlineData("Task<Pageable<int>>")]
        [InlineData("Task<AsyncPageable<int>>")]
        public void InvalidAsyncReturnTypesReportDiagnostic(string returnType)
        {
            var codeFile = BuildCodeFile($$"""
                public virtual {{returnType}} GetValueAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
                """);

            AssertReturnTypeDiagnostic(codeFile);
        }

        [Theory]
        [InlineData("Lookalikes.ClientResult")]
        [InlineData("Lookalikes.ClientResult<int>")]
        [InlineData("Task<Lookalikes.ClientResult<int>>")]
        [InlineData("Lookalikes.CollectionResult")]
        [InlineData("Lookalikes.CollectionResult<int>")]
        [InlineData("Lookalikes.AsyncCollectionResult")]
        [InlineData("Lookalikes.AsyncCollectionResult<int>")]
        [InlineData("Lookalikes.Task<ClientResult>")]
        [InlineData("Lookalikes.Response")]
        [InlineData("Lookalikes.Response<int>")]
        [InlineData("Lookalikes.NullableResponse<int>")]
        [InlineData("Lookalikes.Operation<int>")]
        [InlineData("Lookalikes.Pageable<int>")]
        [InlineData("Lookalikes.AsyncPageable<int>")]
        [InlineData("Nested.System.ClientModel.ClientResult")]
        [InlineData("System.ClientModel.Nested.ClientResult")]
        public void NamespaceLookalikesReportDiagnostic(string returnType)
        {
            var codeFile = BuildCodeFile($$"""
                public virtual {{returnType}} GetValueAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
                """, """
                namespace Lookalikes
                {
                    public class ClientResult { }
                    public class ClientResult<T> { }
                    public class CollectionResult { }
                    public class CollectionResult<T> { }
                    public class AsyncCollectionResult { }
                    public class AsyncCollectionResult<T> { }
                    public class Task<T> { }
                    public class Response { }
                    public class Response<T> { }
                    public class NullableResponse<T> { }
                    public class Operation<T> { }
                    public class Pageable<T> { }
                    public class AsyncPageable<T> { }
                }
                namespace Nested.System.ClientModel
                {
                    public class ClientResult { }
                }
                namespace System.ClientModel.Nested
                {
                    public class ClientResult { }
                }
                """);

            AssertReturnTypeDiagnostic(codeFile);
        }

        [Fact]
        public void InvalidSynchronousCounterpartReportsDiagnostic()
        {
            var codeFile = BuildCodeFile("""
                public virtual string GetValue(int value, CancellationToken cancellationToken = default) => throw new NotImplementedException();
                public virtual Task<ClientResult> GetValueAsync(int asyncValue, CancellationToken cancellationToken = default) => throw new NotImplementedException();
                """);

            var diagnostic = AssertReturnTypeDiagnostic(codeFile);
            Assert.Contains("found string instead", diagnostic.Text);
        }

        [Fact]
        public void InvalidSyncAndAsyncReturnTypesReportBothDiagnostics()
        {
            var codeFile = BuildCodeFile("""
                public virtual string GetValue(CancellationToken cancellationToken = default) => throw new NotImplementedException();
                public virtual Task<int> GetValueAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
                """);

            var diagnostics = codeFile.Diagnostics.Where(diagnostic => diagnostic.DiagnosticId == "AZC0015").ToArray();
            Assert.Equal(2, diagnostics.Length);
            Assert.Contains(diagnostics, diagnostic => diagnostic.Text.Contains("found string instead"));
            Assert.Contains(diagnostics, diagnostic => diagnostic.Text.Contains("found System.Threading.Tasks.Task<int> instead"));
        }

        [Theory]
        [InlineData("DerivedCollectionResult", "CollectionResult<ProjectAgentSession>")]
        [InlineData("DerivedAsyncCollectionResult", "AsyncCollectionResult<ProjectAgentSession>")]
        [InlineData("DerivedResponse", "Response")]
        public void InheritedResultTypesHaveNoReturnTypeDiagnostics(string returnType, string baseType)
        {
            var codeFile = BuildCodeFile($$"""
                public virtual {{returnType}} GetValueAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
                """, $$"""
                namespace Azure.AnalyzerTests
                {
                    public abstract class {{returnType}} : {{baseType}} { }
                }
                """);

            Assert.DoesNotContain(codeFile.Diagnostics, diagnostic => diagnostic.DiagnosticId == "AZC0015");
        }

        [Fact]
        public void InheritedClientResultsHaveNoDiagnostics()
        {
            var codeFile = BuildCodeFile("""
                public virtual CustomResult GetValue(CancellationToken cancellationToken = default) => throw new NotImplementedException();
                public virtual Task<CustomResult> GetValueAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
                public virtual CustomResult<SessionLogEvent> GetEvent(CancellationToken cancellationToken = default) => throw new NotImplementedException();
                public virtual Task<CustomResult<SessionLogEvent>> GetEventAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
                """, """
                namespace Azure.AnalyzerTests
                {
                    public class CustomResult : ClientResult
                    {
                        public CustomResult() : base(null) { }
                    }
                    public class CustomResult<T> : ClientResult<T>
                    {
                        public CustomResult(T value) : base(value, null) { }
                    }
                }
                """);

            Assert.Empty(codeFile.Diagnostics);
        }

        [Fact]
        public void UnrelatedMembersHaveNoReturnTypeDiagnostics()
        {
            var codeFile = BuildCodeFile("""
                public virtual string GetValue(string value, CancellationToken cancellationToken = default) => throw new NotImplementedException();
                public virtual Task<ClientResult> GetValueAsync(int value, CancellationToken cancellationToken = default) => throw new NotImplementedException();
                public virtual bool IsReadyAsync { get; }
                """, """
                namespace Azure.AnalyzerTests
                {
                    public class SessionOperations
                    {
                        public Task<int> GetValueAsync() => throw new NotImplementedException();
                    }
                    internal class InternalClient
                    {
                        public Task<int> GetValueAsync() => throw new NotImplementedException();
                    }
                }
                """);

            Assert.DoesNotContain(codeFile.Diagnostics, diagnostic => diagnostic.DiagnosticId == "AZC0015");
        }

        [Fact]
        public void ExistingAnalyzersStillReportDiagnostics()
        {
            var codeFile = BuildCodeFile("""
                public Task<Response> GetValueAsync() => throw new NotImplementedException();
                """, """
                namespace Azure.AnalyzerTests
                {
                    public class Widget { }
                    public class AnotherClient
                    {
                        public AnotherClient(string endpoint) { }
                    }
                }
                """);

            Assert.Contains(codeFile.Diagnostics, diagnostic => diagnostic.DiagnosticId == "AZC0002");
            Assert.Contains(codeFile.Diagnostics, diagnostic => diagnostic.DiagnosticId == "AZC0003");
            Assert.Contains(codeFile.Diagnostics, diagnostic => diagnostic.DiagnosticId == "AZC0005");
            Assert.Contains(codeFile.Diagnostics, diagnostic => diagnostic.DiagnosticId == "AZC0012");
            Assert.DoesNotContain(codeFile.Diagnostics, diagnostic => diagnostic.DiagnosticId == "AZC0015");
        }

        [Fact]
        public void AnalysisCanBeDisabled()
        {
            var codeFile = BuildCodeFile("""
                public virtual Task<int> GetValueAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
                """, runAnalysis: false);

            Assert.Empty(codeFile.Diagnostics);
        }

        private static CodeDiagnostic AssertReturnTypeDiagnostic(CodeFile codeFile)
        {
            var diagnostic = Assert.Single(codeFile.Diagnostics.Where(diagnostic => diagnostic.DiagnosticId == "AZC0015"));
            Assert.Contains("System.ClientModel", diagnostic.Text);
            Assert.Contains("instead", diagnostic.Text);
            Assert.Equal("Azure.AnalyzerTests.SampleClient", diagnostic.TargetId);
            Assert.Contains(codeFile.ReviewLines.SelectMany(line => line.Children), line => line.LineId == diagnostic.TargetId);
            return diagnostic;
        }

        private static CodeFile BuildCodeFile(string members, string additionalTypes = "", bool runAnalysis = true)
        {
            var source = $$"""
                using System;
                using System.ClientModel;
                using System.Threading;
                using System.Threading.Tasks;
                using Azure;

                namespace Azure.AnalyzerTests
                {
                    public class SampleClient
                    {
                        protected SampleClient() { }
                        {{members}}
                    }
                    public class SessionLogEvent { }
                    public class ProjectAgentSession { }
                    public enum AgentListOrder { Ascending, Descending }
                }
                {{additionalTypes}}
                """;
            var compilation = CSharpCompilation.Create(
                "Azure.AnalyzerTests",
                new[] { CSharpSyntaxTree.ParseText(source) },
                _references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
            stream.Position = 0;

            // Exercise the same metadata-symbol path as parsing an SDK assembly, not just source symbols.
            var assembly = CompilationFactory.GetCompilation(stream, null);
            return new CodeFileBuilder().Build(assembly, runAnalysis, null);
        }
    }
}
