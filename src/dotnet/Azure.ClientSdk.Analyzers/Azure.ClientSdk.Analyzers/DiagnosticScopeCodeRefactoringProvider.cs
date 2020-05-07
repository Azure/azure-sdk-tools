// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

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

                string clientDiagnosticsMember = null;
                foreach (var member in method.ContainingType.GetMembers())
                {
                    if (
                        (member is IFieldSymbol fieldSymbol && SymbolEqualityComparer.Default.Equals(clientDiagnosticsType, fieldSymbol.Type)) ||
                        (member is IPropertySymbol propertySymbol && SymbolEqualityComparer.Default.Equals(clientDiagnosticsType, propertySymbol.Type))
                    )
                    {
                        clientDiagnosticsMember = member.Name;
                    }
                }

                if (clientDiagnosticsMember == null)
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

                    // Trim Async off the scope name
                    var scopeName = method.Name;
                    if (scopeName.EndsWith(AsyncSuffix))
                    {
                        scopeName = scopeName.Substring(0, scopeName.Length - AsyncSuffix.Length);
                    }

                    // $"{nameof(Type}}.{nameof(Method)}"
                    var interpolatedStringParts = new InterpolatedStringContentSyntax[]
                    {
                        SyntaxFactory.Interpolation((ExpressionSyntax) generator.NameOfExpression(generator.IdentifierName(method.ContainingType.Name))),
                        SyntaxFactory.InterpolatedStringText(SyntaxFactory.Token(SyntaxTriviaList.Empty, SyntaxKind.InterpolatedStringTextToken, ".", ".", SyntaxTriviaList.Empty)),
                        SyntaxFactory.Interpolation((ExpressionSyntax) generator.NameOfExpression(generator.IdentifierName(scopeName)))
                    };

                    var initializer = generator.InvocationExpression(
                        generator.MemberAccessExpression(generator.IdentifierName(clientDiagnosticsMember), "CreateScope"),
                        SyntaxFactory.InterpolatedStringExpression(
                            SyntaxFactory.Token(SyntaxKind.InterpolatedStringStartToken),
                            SyntaxFactory.List(interpolatedStringParts),
                            SyntaxFactory.Token(SyntaxKind.InterpolatedStringEndToken)
                        )
                    );

                    var declaration = (LocalDeclarationStatementSyntax) generator.LocalDeclarationStatement(diagnosticScopeType, ScopeVariableName, initializer);
                    declaration = declaration.WithUsingKeyword(SyntaxFactory.Token(SyntaxKind.UsingKeyword));

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

                    return Task.FromResult(context.Document.WithSyntaxRoot(tree.ReplaceNode(body, SyntaxFactory.Block(preconditions))));
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
}