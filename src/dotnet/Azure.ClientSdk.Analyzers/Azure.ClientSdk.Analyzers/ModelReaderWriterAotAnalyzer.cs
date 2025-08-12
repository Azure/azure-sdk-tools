// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using static Azure.ClientSdk.Analyzers.Descriptors;

namespace Azure.ClientSdk.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ModelReaderWriterAotAnalyzer : DiagnosticAnalyzer
    {
        private const string MrwNamespace = "System.ClientModel.Primitives";
        private const string ModelReaderWriterTypeName = "ModelReaderWriter";
        private const string ModelReaderWriterContextTypeName = "ModelReaderWriterContext";
        private const string ReadMethodName = "Read";
        private const string WriteMethodName = "Write";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(AZC0150);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        }

        private void AnalyzeInvocation(OperationAnalysisContext context)
        {
            var invocation = (IInvocationOperation)context.Operation;
            var method = invocation.TargetMethod;

            // Check if the method is part of a ModelReaderWriter class
            // verify namespace is System.ClientModel.Primitives
            if (method.ContainingType?.Name.Equals(ModelReaderWriterTypeName, StringComparison.Ordinal) == false ||
                method.ContainingType?.ContainingNamespace?.ToString().Equals(MrwNamespace, StringComparison.Ordinal) == false)
            {
                return;
            }

            // Check if the method is Read or Write
            if (!method.Name.Equals(ReadMethodName, StringComparison.Ordinal) && !method.Name.Equals(WriteMethodName, StringComparison.Ordinal))
            {
                return;
            }

            // Check if the last parameter is ModelReaderWriterContext or a derived type
            var lastParameter = method.Parameters.LastOrDefault();
            if (lastParameter == null || !IsModelReaderWriterContextType(lastParameter.Type, context.Compilation))
            {
                context.ReportDiagnostic(Diagnostic.Create(AZC0150, invocation.Syntax.GetLocation(), method.Name));
            }
        }

        /// <summary>
        /// Checks if the given type is ModelReaderWriterContext or derived from it.
        /// </summary>
        /// <param name="type">Type to check</param>
        /// <param name="compilation">Current compilation</param>
        /// <returns>True if the type is ModelReaderWriterContext or derived from it</returns>
        private static bool IsModelReaderWriterContextType(ITypeSymbol type, Compilation compilation)
        {
            if (type == null)
            {
                return false;
            }

            // Direct name match check for performance
            if (type.Name.Equals(ModelReaderWriterContextTypeName, StringComparison.Ordinal) &&
                type.ContainingNamespace?.ToString().Equals(MrwNamespace, StringComparison.Ordinal) == true)
            {
                return true;
            }

            // Check base types for inheritance
            var currentType = type.BaseType;
            while (currentType != null)
            {
                if (currentType.Name.Equals(ModelReaderWriterContextTypeName, StringComparison.Ordinal) &&
                    currentType.ContainingNamespace?.ToString().Equals(MrwNamespace, StringComparison.Ordinal) == true)
                {
                    return true;
                }
                currentType = currentType.BaseType;
            }

            // Could also check interfaces if ModelReaderWriterContext is an interface
            return false;
        }
    }
}
