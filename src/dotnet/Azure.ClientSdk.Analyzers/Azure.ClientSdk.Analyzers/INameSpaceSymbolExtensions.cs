// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;

namespace Azure.ClientSdk.Analyzers
{
    public static class INamespaceSymbolExtensions
    {
        public static string GetFullNamespaceName(this INamespaceSymbol namespaceSymbo)
        {
            if (namespaceSymbo is { ContainingNamespace: not null and { IsGlobalNamespace: false} })
            {
                return $"{namespaceSymbo.ContainingNamespace.GetFullNamespaceName()}.{namespaceSymbo.Name}";
            }

            return namespaceSymbo.Name;
        }
    }
}
