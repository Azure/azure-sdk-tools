// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Azure.ClientSdk.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class AddConfigureAwaitAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(Descriptors.AZC0011);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeAwaitExpression, SyntaxKind.AwaitExpression);
        }

        private void AnalyzeAwaitExpression(SyntaxNodeAnalysisContext context)
        {
            var invocation = (AwaitExpressionSyntax)context.Node;
            var operation = context.SemanticModel.GetOperation(invocation, context.CancellationToken);
            if (!(operation is IAwaitOperation awaitOperation)) {
                return;
            }

            if (awaitOperation.Operation is IInvocationOperation configureAwaitOperation)
            {
                if (IsConfigureAwait(configureAwaitOperation.TargetMethod, context.Compilation))
                {
                    return;
                }

                if (!IsTaskType(configureAwaitOperation.Type, context.Compilation))
                {
                    return;
                }
            }

            var location = awaitOperation.Syntax.GetLocation();
            var diagnostic = Diagnostic.Create(Descriptors.AZC0011, location);
            context.ReportDiagnostic(diagnostic);
        }

        private static bool IsConfigureAwait(IMethodSymbol method, Compilation compilation) 
            => method.Name == "ConfigureAwait" && IsTaskType(method.ReceiverType, compilation);

        private static bool IsTaskType(ITypeSymbol type, Compilation compilation) 
        {
            if (type == null) 
            {
                return false;
            }

            var (task, taskOfT, valueTask, valueTaskOfT) = GetTaskTypes(compilation);

            if (task == null || taskOfT == null)
            {
                return false;
            }

            if (type.Equals(task) || type.Equals(valueTask)) 
            {
                return true;
            }

            var originalDefinition = type.OriginalDefinition;
            if (Equals(originalDefinition, taskOfT) || Equals(originalDefinition, valueTaskOfT)) 
            {
                return true;
            }

            if (type.TypeKind == TypeKind.Error) 
            {
                return type.Name.Equals("Task") || type.Name.Equals("ValueTask");
            }

            return false;
        }

        private static (INamedTypeSymbol task, INamedTypeSymbol taskOfT, INamedTypeSymbol valueTask, INamedTypeSymbol valueTaskOfT) GetTaskTypes(Compilation compilation) =>
        (
            compilation.GetTypeByMetadataName(typeof(Task).FullName),
            compilation.GetTypeByMetadataName(typeof(Task<>).FullName),
            compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask"),
            compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1")
        );
    }
}
