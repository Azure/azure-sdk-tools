// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ApiView
{
    public interface ICodeFileBuilderSymbolOrderProvider
    {
        IEnumerable<T> OrderTypes<T>(IEnumerable<T> symbols) where T: ITypeSymbol;
        IEnumerable<ISymbol> OrderMembers(IEnumerable<ISymbol> members);
    }
}