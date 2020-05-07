// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Azure.ClientSdk.Analyzers
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp)]
    public class DiagnosticScopeCodeRefactoringProvider : CodeRefactoringProvider
    {
        private const string ScopeVariableName = "scope";
        private const string AsyncSuffix = "Async";

        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var cancellationToken = context.CancellationToken;
            var semanticModel = await context.Document.GetSemanticModelAsync(cancellationToken);

            var tree = await context.Document.GetSyntaxRootAsync(cancellationToken);
            if (tree == null || semanticModel == null)
            {
                return;
            }

            var node = tree.FindNode(context.Span);
            if (node is MethodDeclarationSyntax methodDeclarationSyntax)
            {
                var method = ModelExtensions.GetDeclaredSymbol(semanticModel, methodDeclarationSyntax) as IMethodSymbol;
                var body = methodDeclarationSyntax.Body;
                if (method == null ||
                    method.IsStatic ||
                    body == null)
                {
                    return;
                }

                var diagnosticScopeType = semanticModel.Compilation.GetTypeByMetadataName("Azure.Core.Pipeline.DiagnosticScope");
                var clientDiagnosticsType = semanticModel.Compilation.GetTypeByMetadataName("Azure.Core.Pipeline.ClientDiagnostics");
                var exceptionType = semanticModel.Compilation.GetTypeByMetadataName("System.Exception");

                if (diagnosticScopeType == null || clientDiagnosticsType == null || exceptionType == null)
                {
                    return;
                }

                Task<Document> AddDiagnosticScope()
                {
                    var generator = SyntaxGenerator.GetGenerator(context.Document);

                    var preconditions = new List<StatementSyntax>();
                    var mainLogic = new List<SyntaxNode>();

                    foreach (var statement in body.Statements)
                    {
                        if (mainLogic.Count == 0 &&
                            IsPrecondition(statement))
                        {
                            preconditions.Add(statement);
                        }
                        else
                        {
                            mainLogic.Add(statement);
                        }
                    }

                    var scopeName = method.Name;
                    if (scopeName.EndsWith(AsyncSuffix))
                    {
                        scopeName = scopeName.Substring(0, scopeName.Length - AsyncSuffix.Length);
                    }
                    var interpolatedStringParts = new InterpolatedStringContentSyntax[]
                    {
                        Interpolation((ExpressionSyntax) generator.NameOfExpression(generator.IdentifierName(method.ContainingType.Name))),
                        InterpolatedStringText(Token(SyntaxTriviaList.Empty, SyntaxKind.InterpolatedStringTextToken, ".", ".", SyntaxTriviaList.Empty)),
                        Interpolation((ExpressionSyntax) generator.NameOfExpression(generator.IdentifierName(scopeName)))
                    };

                    var initializer = generator.InvocationExpression(
                        generator.MemberAccessExpression(generator.IdentifierName("_clientDiagnostics"), "CreateScope"),
                        InterpolatedStringExpression(
                            Token(SyntaxKind.InterpolatedStringStartToken),
                            List(interpolatedStringParts),
                            Token(SyntaxKind.InterpolatedStringEndToken)
                        )
                    );

                    var declaration = (LocalDeclarationStatementSyntax) generator.LocalDeclarationStatement(diagnosticScopeType, ScopeVariableName, initializer);
                    declaration = declaration.WithUsingKeyword(Token(SyntaxKind.UsingKeyword));

                    preconditions.Add(declaration);
                    preconditions.Add(
                        (StatementSyntax) generator.ExpressionStatement(
                            generator.InvocationExpression(
                                generator.MemberAccessExpression(generator.IdentifierName(ScopeVariableName), "Start"))));

                    preconditions.Add(
                        (StatementSyntax) generator.TryCatchStatement(
                            mainLogic,
                            generator.CatchClause(exceptionType, "ex", new[]
                            {
                                generator.ExpressionStatement(
                                    generator.InvocationExpression(
                                        generator.MemberAccessExpression(generator.IdentifierName(ScopeVariableName), "Failed"),
                                        generator.Argument(generator.IdentifierName("ex")))),
                                generator.ThrowStatement()
                            })
                        ));

                    return Task.FromResult(context.Document.WithSyntaxRoot(tree.ReplaceNode(body, Block(preconditions))));
                }

                context.RegisterRefactoring(CodeAction.Create("Add diagnostic scope", token => AddDiagnosticScope()));
            }
        }

        private bool IsPrecondition(StatementSyntax statement)
        {
            foreach (var node in statement.DescendantNodes())
            {
                // Handle ArgumentExceptions and Argument.* checks
                if (node is IdentifierNameSyntax identifierNameSyntax && identifierNameSyntax.Identifier.Text.Contains("Argument"))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public abstract class ClientAnalyzerBase : SymbolAnalyzerBase
    {
        protected const string ClientSuffix = "Client";

        public override SymbolKind[] SymbolKinds { get; } = new[] {SymbolKind.NamedType};

        public override void Analyze(ISymbolAnalysisContext context)
        {
            var typeSymbol = (INamedTypeSymbol) context.Symbol;
            if (typeSymbol.TypeKind != TypeKind.Class || !typeSymbol.Name.EndsWith(ClientSuffix) || typeSymbol.DeclaredAccessibility != Accessibility.Public)
            {
                return;
            }

            AnalyzeCore(context);
        }

        protected class ParameterEquivalenceComparer : IEqualityComparer<IParameterSymbol>, IEqualityComparer<ITypeParameterSymbol>
        {
            public static ParameterEquivalenceComparer Default { get; } = new ParameterEquivalenceComparer();

            public bool Equals(IParameterSymbol x, IParameterSymbol y)
            {
                return SymbolEqualityComparer.Default.Equals(x.Type, y.Type) && x.Name.Equals(y.Name);
            }

            public int GetHashCode(IParameterSymbol obj)
            {
                return obj.Type.GetHashCode() ^ obj.Name.GetHashCode();
            }

            public bool Equals(ITypeParameterSymbol x, ITypeParameterSymbol y)
            {
                return x.Name.Equals(y.Name);
            }

            public int GetHashCode(ITypeParameterSymbol obj)
            {
                return obj.Name.GetHashCode();
            }
        }

        protected static IMethodSymbol FindMethod(IEnumerable<IMethodSymbol> methodSymbols, ImmutableArray<ITypeParameterSymbol> genericParameters, ImmutableArray<IParameterSymbol> parameters)
        {
            return methodSymbols.SingleOrDefault(symbol =>
                genericParameters.SequenceEqual(symbol.TypeParameters, ParameterEquivalenceComparer.Default) &&
                parameters.SequenceEqual(symbol.Parameters, ParameterEquivalenceComparer.Default));
        }

        protected static IMethodSymbol FindMethod(IEnumerable<IMethodSymbol> methodSymbols, ImmutableArray<ITypeParameterSymbol> genericParameters, ImmutableArray<IParameterSymbol> parameters, Func<IParameterSymbol, bool> lastParameter)
        {
            return methodSymbols.SingleOrDefault(symbol =>
            {
                if (!symbol.Parameters.Any() || !genericParameters.SequenceEqual(symbol.TypeParameters, ParameterEquivalenceComparer.Default))
                {
                    return false;
                }

                var allButLast = symbol.Parameters.RemoveAt(symbol.Parameters.Length - 1);

                return allButLast.SequenceEqual(parameters, ParameterEquivalenceComparer.Default) && lastParameter(symbol.Parameters.Last());
            });
        }

        public abstract void AnalyzeCore(ISymbolAnalysisContext context);
    }
}