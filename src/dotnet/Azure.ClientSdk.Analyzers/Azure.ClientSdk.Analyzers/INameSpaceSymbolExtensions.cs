// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Azure.ClientSdk.Analyzers
{
    public static class INamespaceSymbolExtensions
    {
        public static string GetFullNamespaceName(this INamespaceSymbol namespaceSymbol)
        {
            return string.Join(".", namespaceSymbol.GetAllNamespaces());
        }

        internal static IReadOnlyList<string> GetAllNamespaces(this INamespaceSymbol namespaceSymbol)
        {
            var namespaces = new List<string>();
            while (namespaceSymbol is { IsGlobalNamespace: false })
            {
                namespaces.Add(namespaceSymbol.Name);
                namespaceSymbol = namespaceSymbol.ContainingNamespace;
            }
            namespaces.Reverse();
            return namespaces;
        }
    }
}
