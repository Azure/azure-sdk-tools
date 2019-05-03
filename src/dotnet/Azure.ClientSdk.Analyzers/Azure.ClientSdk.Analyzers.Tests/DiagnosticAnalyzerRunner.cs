// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class DiagnosticAnalyzerRunner
    {
        public DiagnosticAnalyzerRunner(DiagnosticAnalyzer analyzer)
        {
            Analyzer = analyzer;
        }

        public DiagnosticAnalyzer Analyzer { get; }

        public Task<Diagnostic[]> GetDiagnosticsAsync(string source)
        {
            var project = DiagnosticProject.Create(GetType().Assembly, new[] { source });
            return GetDiagnosticsAsync(new [] { project }, Analyzer);
        }

        protected async Task<Diagnostic[]> GetDiagnosticsAsync(
            IEnumerable<Project> projects,
            DiagnosticAnalyzer analyzer)
        {
            var diagnostics = new List<Diagnostic>();
            foreach (var project in projects)
            {
                var compilation = await project.GetCompilationAsync();

                // Enable any additional diagnostics
                var options = ConfigureCompilationOptions(compilation.Options);
                var compilationWithAnalyzers = compilation
                    .WithOptions(options)
                    .WithAnalyzers(ImmutableArray.Create(analyzer));

                var diags = await compilationWithAnalyzers.GetAllDiagnosticsAsync();

                Assert.DoesNotContain(diags, d => d.Id == "AD0001");

                // Filter out non-error diagnostics not produced by our analyzer
                // We want to KEEP errors because we might have written bad code. But sometimes we leave warnings in to make the
                // test code more convenient
                diags = diags.Where(d => d.Severity == DiagnosticSeverity.Error || analyzer.SupportedDiagnostics.Any(s => s.Id.Equals(d.Id))).ToImmutableArray();

                foreach (var diag in diags)
                {
                    if (diag.Location == Location.None || diag.Location.IsInMetadata)
                    {
                        diagnostics.Add(diag);
                    }
                    else
                    {
                        foreach (var document in projects.SelectMany(p => p.Documents))
                        {
                            var tree = await document.GetSyntaxTreeAsync();
                            if (tree == diag.Location.SourceTree)
                            {
                                diagnostics.Add(diag);
                            }
                        }
                    }
                }
            }

            return diagnostics.OrderBy(d => d.Location.SourceSpan.Start).ToArray();
        }

        protected virtual CompilationOptions ConfigureCompilationOptions(CompilationOptions options)
        {
            return options.WithOutputKind(OutputKind.DynamicallyLinkedLibrary);
        }
    }
}
