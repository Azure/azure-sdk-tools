// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Azure.ClientSdk.Analyzers
{
    public abstract class ClientAnalyzerBase : SymbolAnalyzerBase
    {
        protected const string ClientSuffix = "Client";

        public override SymbolKind[] SymbolKinds { get; } = new[] { SymbolKind.NamedType };

        public override void Analyze(ISymbolAnalysisContext context)
        {
            var typeSymbol = (INamedTypeSymbol)context.Symbol;
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
                return x.Name.Equals(y.Name) && TypeSymbolEquals(x.Type, y.Type);
            }

            private bool TypeSymbolEquals(ITypeSymbol x, ITypeSymbol y)
            {
                switch (x)
                {
                    case INamedTypeSymbol namedX when namedX.IsGenericType:
                        var namedY = y as INamedTypeSymbol;
                        if (namedY == null || !namedY.IsGenericType)
                            return false;

                        if (!namedX.MetadataName.Equals(namedY.MetadataName) || namedX.TypeArguments.Length != namedY.TypeArguments.Length)
                            return false;

                        for (int i = 0; i < namedX.TypeArguments.Length; i++)
                        {
                            var areEqual = TypeSymbolEquals(namedX.TypeArguments[i], namedY.TypeArguments[i]);
                            if (!areEqual)
                                return false;
                        }
                        return true;

                    case ITypeSymbol typeSym when typeSym.TypeKind == TypeKind.TypeParameter:
                        return y.TypeKind == TypeKind.TypeParameter && x.Name.Equals(y.Name);

                    default:
                        return SymbolEqualityComparer.Default.Equals(x, y) && x.Name.Equals(y.Name);
                }
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

        protected static IMethodSymbol FindMethod(IEnumerable<IMethodSymbol> methodSymbols, ImmutableArray<ITypeParameterSymbol> genericParameters, ImmutableArray<IParameterSymbol> parameters, bool ignoreStatic = false)
        {
            return methodSymbols.SingleOrDefault(symbol =>
                (!ignoreStatic || !symbol.IsStatic) &&
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
