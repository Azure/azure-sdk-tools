// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using static Azure.ClientSdk.Analyzers.Descriptors;

namespace Azure.ClientSdk.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class AsyncAnalyzer : DiagnosticAnalyzer
    {
        private AsyncAnalyzerUtilities _asyncUtilities;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(AZC0100, AZC0101, AZC0102, AZC0103, AZC0104, AZC0105, AZC0106, AZC0107, AZC0108, AZC0109);

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
            context.RegisterSyntaxNodeAction(AnalyzeUsingDeclarationExpression, SyntaxKind.LocalDeclarationStatement);
            context.RegisterSyntaxNodeAction(AnalyzeArrowExpressionClause, SyntaxKind.ArrowExpressionClause);

            context.RegisterOperationAction(AnalyzeAnonymousFunction, OperationKind.AnonymousFunction);
            context.RegisterOperationAction(AnalyzeMethodBody, OperationKind.MethodBody);
        }

        private void AnalyzeArrowExpressionClause(SyntaxNodeAnalysisContext context) 
        {
            if (!(context.ContainingSymbol is IMethodSymbol method) || method.MethodKind != MethodKind.PropertyGet) 
            {
                return;
            }

            var operation = context.SemanticModel.GetOperation(context.Node, context.CancellationToken);
            if (operation is IBlockOperation block && block.Parent == null) 
            {
                new  MethodBodyAnalyzer(context.ReportDiagnostic, context.Compilation, _asyncUtilities).Run(method, block);
            }
        }

        private void AnalyzeMethodBody(OperationAnalysisContext context) 
        {
            var method = (IMethodSymbol) context.ContainingSymbol;
            var methodBody = (IMethodBodyOperation)context.Operation;
            new MethodBodyAnalyzer(context.ReportDiagnostic, context.Compilation, _asyncUtilities).Run(method, methodBody.BlockBody ?? methodBody.ExpressionBody);
        }

        private void AnalyzeAnonymousFunction(OperationAnalysisContext context) 
        {
            var operation = (IAnonymousFunctionOperation) context.Operation;
            var method = operation.Symbol;
            if (method.ContainingSymbol.Kind != SymbolKind.Method) 
            {
                new  MethodBodyAnalyzer(context.ReportDiagnostic, context.Compilation, _asyncUtilities).Run(method, operation.Body);
            }
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

        private static void ReportConfigureAwaitDiagnostic(SyntaxNodeAnalysisContext context, IOperation operation) 
        {
            var location = operation.Syntax.GetLocation();
            var diagnostic = Diagnostic.Create(Descriptors.AZC0100, location);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
