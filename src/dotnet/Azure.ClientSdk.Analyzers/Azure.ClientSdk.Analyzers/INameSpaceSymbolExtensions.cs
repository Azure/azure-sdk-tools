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
            return string.Join(".", GetAllNamespaces(namespaceSymbol));
        }

        private static IList<string> GetAllNamespaces(INamespaceSymbol namespaceSymbol)
        {
            if (namespaceSymbol is { ContainingNamespace: not null and { IsGlobalNamespace: false } })
            {
                var namespaces = GetAllNamespaces(namespaceSymbol.ContainingNamespace);
                namespaces.Add(namespaceSymbol.Name);
                return namespaces;
            }

            return new List<string> { namespaceSymbol.Name };

        }
    }
}
