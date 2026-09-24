// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace APIView.CSharp
{
    public interface ICodeFileBuilderSymbolOrderProvider
    {
        IEnumerable<T> OrderTypes<T>(IEnumerable<T> symbols) where T : ITypeSymbol;
        IEnumerable<ISymbol> OrderMembers(IEnumerable<ISymbol> members);
        IEnumerable<INamespaceSymbol> OrderNamespaces(IEnumerable<INamespaceSymbol> namespaces);
    }
}