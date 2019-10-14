// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ApiView
{
    class CodeFileBuilderSymbolOrderProvider : ICodeFileBuilderSymbolOrderProvider
    {
        public IEnumerable<T> OrderTypes<T>(IEnumerable<T> symbols) where T : ITypeSymbol
        {
            return symbols.OrderBy(t => (GetTypeOrder(t), t.DeclaredAccessibility != Accessibility.Public, t.Name));
        }

        public IEnumerable<ISymbol> OrderMembers(IEnumerable<ISymbol> members)
        {
            return members.OrderBy(t => (GetMemberOrder(t), t.DeclaredAccessibility != Accessibility.Public, t.Name));
        }

        public IEnumerable<INamespaceSymbol> OrderNamespaces(IEnumerable<INamespaceSymbol> namespaces)
        {
            return namespaces.OrderBy(n => n.ToDisplayString());
        }

        private static int GetTypeOrder(ITypeSymbol typeSymbol)
        {
            if (typeSymbol.Name.EndsWith("Client"))
            {
                return -100;
            }

            if (typeSymbol.Name.EndsWith("Options"))
            {
                return -20;
            }

            if (typeSymbol.Name.EndsWith("Extensions"))
            {
                return 1;
            }

            if (typeSymbol.TypeKind == TypeKind.Interface)
            {
                return -1;
            }
            if (typeSymbol.TypeKind == TypeKind.Enum)
            {
                return 90;
            }
            if (typeSymbol.TypeKind == TypeKind.Delegate)
            {
                return 99;
            }
            if (typeSymbol.Name.EndsWith("Exception"))
            {
                return 100;
            }

            // Nested type
            if (typeSymbol.ContainingType != null)
            {
                return 3;
            }

            return 0;
        }

        private static int GetMemberOrder(ISymbol symbol)
        {
            switch (symbol)
            {
                case IFieldSymbol fieldSymbol when fieldSymbol.ContainingType.TypeKind == TypeKind.Enum:
                    return (int)Convert.ToInt64(fieldSymbol.ConstantValue);

                case IMethodSymbol methodSymbol when methodSymbol.MethodKind == MethodKind.Constructor:
                    return -10;

                case IMethodSymbol methodSymbol when (methodSymbol.OverriddenMethod?.ContainingType?.SpecialType == SpecialType.System_Object ||
                                                      methodSymbol.OverriddenMethod?.ContainingType?.SpecialType == SpecialType.System_ValueType):
                    return 5;
                case IMethodSymbol methodSymbol when methodSymbol.IsStatic:
                    return -4;
                case IPropertySymbol _:
                    return -5;
                case IFieldSymbol _:
                    return -6;
                default:
                    return 0;
            }
        }
    }
}