// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class WhitespaceNewLineAnalyzer: DiagnosticAnalyzer
    {
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(
                analysisContext =>
                {
                    analysisContext.RegisterSyntaxTreeAction(
                        nodeContext => {
                            var rootNode = nodeContext.Tree.GetCompilationUnitRoot(nodeContext.CancellationToken);
                            foreach (var node in rootNode.DescendantTokens())
                            {
                                CheckNewLines(node.LeadingTrivia, nodeContext);
                                CheckNewLines(node.TrailingTrivia, nodeContext);
                            }
                        });
                });
        }

        private static void CheckNewLines(SyntaxTriviaList syntaxTriviaList, SyntaxTreeAnalysisContext nodeContext)
        {
            int newLineCount = 0;
            SyntaxTrivia previousTrivia = default;
            foreach (var trivia in syntaxTriviaList)
            {
                if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                {
                    if (previousTrivia.IsKind(SyntaxKind.WhitespaceTrivia))
                    {
                        nodeContext.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC1002, previousTrivia.GetLocation()));
                    }

                    if (newLineCount > 0)
                    {
                        nodeContext.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC1001, trivia.GetLocation()));
                    }

                    newLineCount++;
                }
                else if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia))
                {
                    // Reset new line count on comments and other stuff
                    newLineCount = -1;
                }

                previousTrivia = trivia;
            }
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(new[]
        {
            Descriptors.AZC1001,
            Descriptors.AZC1002
        });
    }
}