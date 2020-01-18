// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Azure.ClientSdk.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class AddConfigureAwaitAnalyzer : DiagnosticAnalyzer
    {
        private AsyncAnalyzerUtilities _asyncUtilities;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(Descriptors.AZC0012, Descriptors.AZC0013);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(CompilationStart);
        }

        private void CompilationStart(CompilationStartAnalysisContext context) 
        {
            _asyncUtilities = new AsyncAnalyzerUtilities(context.Compilation);
            context.RegisterSyntaxNodeAction(AnalyzeAwaitExpression, SyntaxKind.AwaitExpression);
            context.RegisterSyntaxNodeAction(AnalyzeUsingExpression, SyntaxKind.UsingStatement);
            context.RegisterSyntaxNodeAction(AnalyzeForEachExpression, SyntaxKind.ForEachStatement);
            context.RegisterSyntaxNodeAction(AnalyzeConfigureAwaitTrue, SyntaxKind.InvocationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeUsingDeclarationExpression, SyntaxKind.LocalDeclarationStatement);
        }

        private void AnalyzeAwaitExpression(SyntaxNodeAnalysisContext context)
        {
            var operation = context.SemanticModel.GetOperation(context.Node, context.CancellationToken);
            if (!(operation is IAwaitOperation awaitOperation)) {
                return;
            }

            if (_asyncUtilities.IsTaskType(awaitOperation.Operation.Type)) 
            {
                ReportConfigureAwaitDiagnostic(context, awaitOperation);
            }
        }

        private void AnalyzeUsingExpression(SyntaxNodeAnalysisContext context) 
        {
            if (!_asyncUtilities.IsAwaitUsingStatement(context.Node))
            {
                return;
            }

            if (!(context.SemanticModel.GetOperation(context.Node, context.CancellationToken) is IUsingOperation usingOperation))
            {
                return;
            }

            if (!usingOperation.IsAsynchronous)
            {
                return;
            }

            var resources = usingOperation.Resources;
            if (_asyncUtilities.IsAsyncDisposableType(resources.Type))
            {
                ReportConfigureAwaitDiagnostic(context, resources);
            }
        }

        private void AnalyzeUsingDeclarationExpression(SyntaxNodeAnalysisContext context) 
        {
            if (!_asyncUtilities.IsAwaitLocalDeclaration(context.Node))
            {
                return;
            }

            if (!(context.SemanticModel.GetOperation(context.Node, context.CancellationToken) is IUsingDeclarationOperation usingDeclarationOperation))
            {
                return;
            }

            if (!usingDeclarationOperation.IsAsynchronous)
            {
                return;
            }

            var initializes = usingDeclarationOperation.DeclarationGroup.Declarations
                .SelectMany(d => d.Declarators)
                .Select(d => d.Initializer?.Value);

            foreach (var initializer in initializes)
            {
                if (_asyncUtilities.IsAsyncDisposableType(initializer?.Type))
                {
                    ReportConfigureAwaitDiagnostic(context, initializer);
                }
            }
        }

        private void AnalyzeForEachExpression(SyntaxNodeAnalysisContext context) 
        {
            if (!_asyncUtilities.IsAwaitForEach(context.Node)) 
            {
                return;
            }

            if (!(context.SemanticModel.GetOperation(context.Node, context.CancellationToken) is IForEachLoopOperation forEachOperation))
            {
                return;
            }

            if (!forEachOperation.IsAsynchronous)
            {
                return;
            }

            var collectionType = forEachOperation.Collection.Type;
            if (_asyncUtilities.IsAsyncEnumerableType(collectionType)) 
            {
                ReportConfigureAwaitDiagnostic(context, forEachOperation.Collection);
            }
        }

        private void AnalyzeConfigureAwaitTrue(SyntaxNodeAnalysisContext context)
        {
            if (!_asyncUtilities.IsConfigureAwaitInvocation(context.Node))
            {
                return;
            }

            if (!(context.SemanticModel.GetOperation(context.Node, context.CancellationToken) is IInvocationOperation operation)) 
            {
                return;
            }

            if (!_asyncUtilities.IsConfigureAwait(operation.TargetMethod)) 
            {
                return;
            }

            // ConfigureAwait is either an instance method with one parameter or a static extension method with two.
            // We need to check if the last argument is a bool and if it is 'true'
            var constantValue = operation.Arguments.Last().Value?.ConstantValue;
            if (constantValue != null && constantValue.Value.Value is bool value && value)
            {
                ReportConfigureAwaitTrueDiagnostic(context, operation);
            }
        }

        private static void ReportConfigureAwaitDiagnostic(SyntaxNodeAnalysisContext context, IOperation awaitOperation) 
        {
            var location = awaitOperation.Syntax.GetLocation();
            var diagnostic = Diagnostic.Create(Descriptors.AZC0012, location);
            context.ReportDiagnostic(diagnostic);
        }

        private static void ReportConfigureAwaitTrueDiagnostic(SyntaxNodeAnalysisContext context, IOperation configureAwaitOperation) 
        {
            var invocation = (InvocationExpressionSyntax)configureAwaitOperation.Syntax;
            var memberAccess = (MemberAccessExpressionSyntax) invocation.Expression;
            var start = memberAccess.Name.Span.Start;
            var end = invocation.Span.End;
            var diagnostic = Diagnostic.Create(Descriptors.AZC0013, Location.Create(invocation.SyntaxTree, new TextSpan(start, end - start)));
            context.ReportDiagnostic(diagnostic);
        }
    }
}
