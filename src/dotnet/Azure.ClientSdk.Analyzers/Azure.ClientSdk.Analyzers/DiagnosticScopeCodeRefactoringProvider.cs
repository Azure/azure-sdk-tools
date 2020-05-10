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
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Azure.ClientSdk.Analyzers
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp)]
    public class DiagnosticScopeCodeRefactoringProvider : CodeRefactoringProvider
    {

        private const string ScopeVariableName = "scope";
        private const string AsyncSuffix = "Async";
        private const string AzureCorePipelineClientDiagnosticsTypeName = "Azure.Core.Pipeline.ClientDiagnostics";
        private const string AzureCorePipelineDiagnosticScopeTypeName = "Azure.Core.Pipeline.DiagnosticScope";
        private const string SystemExceptionTypeName = "System.Exception";

        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var cancellationToken = context.CancellationToken;
            var semanticModel = await context.Document.GetSemanticModelAsync(cancellationToken);
            if (semanticModel == null) return;

            var tree = await context.Document.GetSyntaxRootAsync(cancellationToken);
            if (tree == null) return;

            var node = tree.FindNode(context.Span);
            if (!(node is MethodDeclarationSyntax methodDeclarationSyntax))
            {
                return;
            }

            var method = ModelExtensions.GetDeclaredSymbol(semanticModel, methodDeclarationSyntax) as IMethodSymbol;

            if (method == null ||
                method.IsStatic ||
                method.IsAbstract ||
                method.ContainingType.TypeKind != TypeKind.Class)
            {
                return;
            }

            var diagnosticScopeType = semanticModel.Compilation.GetTypeByMetadataName(AzureCorePipelineDiagnosticScopeTypeName);
            var clientDiagnosticsType = semanticModel.Compilation.GetTypeByMetadataName(AzureCorePipelineClientDiagnosticsTypeName);
            var exceptionType = semanticModel.Compilation.GetTypeByMetadataName(SystemExceptionTypeName);

            if (diagnosticScopeType == null ||
                clientDiagnosticsType == null ||
                exceptionType == null)
            {
                return;
            }

            string clientDiagnosticsMember = null;
            foreach (var member in method.ContainingType.GetMembers())
            {
                if ((member is IFieldSymbol fieldSymbol && SymbolEqualityComparer.Default.Equals(clientDiagnosticsType, fieldSymbol.Type)) ||
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

                IEnumerable<StatementSyntax> statements;

                if (methodDeclarationSyntax.Body != null)
                {
                    statements = methodDeclarationSyntax.Body.Statements;
                }
                else if (methodDeclarationSyntax.ExpressionBody != null)
                {
                    statements = new [] { (StatementSyntax)generator.ReturnStatement(methodDeclarationSyntax.ExpressionBody.Expression) };
                }
                else
                {
                    return Task.FromResult(context.Document);
                }

                foreach (var statement in statements)
                {
                    if (mainLogic.Count > 0 ||
                        IncludeInScopeBody(statement))
                    {
                        mainLogic.Add(statement);
                    }
                    else
                    {
                        preconditions.Add(statement);
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
                    Interpolation((ExpressionSyntax) generator.NameOfExpression(generator.IdentifierName(method.ContainingType.Name))),
                    InterpolatedStringText(Token(SyntaxTriviaList.Empty, SyntaxKind.InterpolatedStringTextToken, ".", ".", SyntaxTriviaList.Empty)),
                    Interpolation((ExpressionSyntax) generator.NameOfExpression(generator.IdentifierName(scopeName)))
                };

                var initializer = generator.InvocationExpression(
                    generator.MemberAccessExpression(generator.IdentifierName(clientDiagnosticsMember), "CreateScope"),
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

                var newMethodDeclaration = methodDeclarationSyntax.WithExpressionBody(null)
                    .WithBody(Block(preconditions))
                    .WithSemicolonToken(default);

                return Task.FromResult(context.Document.WithSyntaxRoot(tree.ReplaceNode(methodDeclarationSyntax, newMethodDeclaration)));
            }

            context.RegisterRefactoring(CodeAction.Create("Azure SDK: Add diagnostic scope", token => AddDiagnosticScope()));
        }

        private bool IncludeInScopeBody(StatementSyntax statement)
        {
            foreach (var node in statement.DescendantNodes())
            {
                // Handle ArgumentExceptions and Argument.* checks
                if (node is IdentifierNameSyntax identifierNameSyntax && identifierNameSyntax.Identifier.Text.Contains("Argument"))
                {
                    return false;
                }
            }

            return true;
        }
    }
}