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
            ImmutableArray.Create(AZC0100, AZC0101, AZC0102, AZC0103, AZC0104, AZC0105, AZC0106, AZC0107, AZC0108, AZC0109, AZC0110, AZC0111);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(CompilationStart);
        }

        private void CompilationStart(CompilationStartAnalysisContext context)
        {
            _asyncUtilities = new AsyncAnalyzerUtilities(context.Compilation);

            context.RegisterSyntaxNodeAction(AnalyzeArrowExpressionClause, SyntaxKind.ArrowExpressionClause);

            context.RegisterOperationAction(AnalyzeAwait, OperationKind.Await);
            context.RegisterOperationAction(AnalyzeUsing, OperationKind.Using);
            context.RegisterOperationAction(AnalyzeUsingDeclaration, OperationKind.UsingDeclaration);
            context.RegisterOperationAction(AnalyzeAnonymousFunction, OperationKind.AnonymousFunction);
            context.RegisterOperationAction(AnalyzeMethodBody, OperationKind.MethodBody);
            context.RegisterOperationAction(AnalyzeLoop, OperationKind.Loop);
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
                MethodBodyAnalyzer.Run(context.ReportDiagnostic, context.Compilation, _asyncUtilities, method, block);
            }
        }

        private void AnalyzeMethodBody(OperationAnalysisContext context)
        {
            var method = (IMethodSymbol) context.ContainingSymbol;
            var methodBody = (IMethodBodyOperation)context.Operation;
            MethodBodyAnalyzer.Run(context.ReportDiagnostic, context.Compilation, _asyncUtilities, method, methodBody.BlockBody ?? methodBody.ExpressionBody);
        }

        private void AnalyzeAnonymousFunction(OperationAnalysisContext context)
        {
            var operation = (IAnonymousFunctionOperation) context.Operation;
            var method = operation.Symbol;
            if (method.ContainingSymbol.Kind != SymbolKind.Method)
            {
                MethodBodyAnalyzer.Run(context.ReportDiagnostic, context.Compilation, _asyncUtilities, method, operation.Body);
            }
        }

        private void AnalyzeAwait(OperationAnalysisContext context)
        {
            var awaitOperation = (IAwaitOperation) context.Operation;
            if (_asyncUtilities.IsTaskType(awaitOperation.Operation.Type))
            {
                ReportConfigureAwaitDiagnostic(context, awaitOperation);
            }
        }

        private void AnalyzeUsing(OperationAnalysisContext context)
        {
            var usingOperation = (IUsingOperation) context.Operation;
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

        private void AnalyzeUsingDeclaration(OperationAnalysisContext context)
        {
            var usingDeclarationOperation = (IUsingDeclarationOperation) context.Operation;
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

        private void AnalyzeLoop(OperationAnalysisContext context)
        {
            if (!(context.Operation is IForEachLoopOperation forEachOperation))
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

        private static void ReportConfigureAwaitDiagnostic(OperationAnalysisContext context, IOperation operation)
        {
            var location = operation.Syntax.GetLocation();
            var diagnostic = Diagnostic.Create(AZC0100, location);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
