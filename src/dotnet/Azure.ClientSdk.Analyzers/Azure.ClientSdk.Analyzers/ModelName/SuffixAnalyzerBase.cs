// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers.ModelName
{
    /// <summary>
    /// Base analyzer to check model name suffixes.
    /// </summary>
    public abstract class SuffixAnalyzerBase : DiagnosticAnalyzer
    {
        protected static readonly string Title = "Improper model name suffix";
        protected static readonly string Description = "Suffix is not recommended. Consider to remove or modify it.";
        protected static readonly string GeneralRenamingMessageFormat = "Model name '{0}' ends with '{1}'. Suggest to rename it to an appropriate name.";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray<DiagnosticDescriptor>.Empty;

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
        }

        private void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var typeSymbol = (INamedTypeSymbol)context.Symbol;
            if (!IsClassUnderModelsNamespace(typeSymbol) || AnalyzerUtils.IsNotSdkCode(typeSymbol) || ShouldSkip(typeSymbol, context))
                return;

            foreach (var suffix in SuffixesToCatch)
            {
                if (typeSymbol.Name.EndsWith(suffix))
                {
                    context.ReportDiagnostic(GetDiagnostic(typeSymbol, suffix, context));
                    return;
                }
            }
        }

        protected abstract bool ShouldSkip(INamedTypeSymbol symbol, SymbolAnalysisContext context);
        protected abstract string[] SuffixesToCatch { get; }
        protected abstract Diagnostic GetDiagnostic(INamedTypeSymbol typeSymbol, string suffix, SymbolAnalysisContext context);

        // check if a given symbol is the sub-type of the given type
        protected bool IsTypeOf(INamedTypeSymbol typeSymbol, string namespaceName, string typeName)
        {
            if (typeSymbol is null)
                return false;

            // check class hierachy
            for (var classType = typeSymbol; classType is not null; classType = classType.BaseType)
            {
                if (classType.Name == typeName && classType.ContainingNamespace.GetFullNamespaceName() == namespaceName)
                    return true;
            };

            // check interfaces
            return typeSymbol.AllInterfaces.Any(i => i.Name == typeName && i.ContainingNamespace.Name == namespaceName);
        }

        private bool IsClassUnderModelsNamespace(ITypeSymbol symbol) => symbol is { TypeKind: TypeKind.Class } && HasModelsNamespace(symbol);

        private bool HasModelsNamespace(ITypeSymbol typeSymbol)
        {
            for (var namespaceSymbol = typeSymbol.ContainingNamespace; namespaceSymbol != null; namespaceSymbol = namespaceSymbol.ContainingNamespace)
            {
                if (namespaceSymbol.Name == "Models")
                    return true;
            }
            return false;
        }
    }
}

