// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Azure.ClientSdk.Analyzers
{
    internal class AnalyzerUtils
    {
        internal static bool IsNotSdkCode(ISymbol symbol) => !IsSdkCode(symbol);

        internal static bool IsSdkCode(ISymbol symbol)
        {
            var namespaces = symbol.ContainingNamespace.GetAllNamespaces();

            return IsSdkNamespace(namespaces);
        }

        internal static bool IsNotSdkCode(SyntaxNode node, SemanticModel model) => !IsSdkCode(node, model);

        internal static bool IsSdkCode(SyntaxNode node, SemanticModel model)
        {
            var symbol = model.GetDeclaredSymbol(node);
            if (symbol != null)
            {
                return IsSdkCode(symbol);
            }

            var namespaces = GetNamespace(node);
            return IsSdkNamespace(namespaces);
        }

        private static bool IsSdkNamespace(IReadOnlyList<string> namespaces) => namespaces.Count >= 2 && namespaces[0] == "Azure" && namespaces[1] != "Core";

        private static IReadOnlyList<string> GetNamespace(SyntaxNode node)
        {
            var namespaces = new List<string>();

            var parent = node.Parent;

            while (parent != null &&
                    parent is not NamespaceDeclarationSyntax
                    && parent is not FileScopedNamespaceDeclarationSyntax)
            {
                parent = parent.Parent;
            }

            if (parent is BaseNamespaceDeclarationSyntax namespaceParent)
            {
                namespaces.Add(namespaceParent.Name.ToString());

                while (true)
                {
                    if (namespaceParent.Parent is not NamespaceDeclarationSyntax parentNamespace)
                    {
                        break;
                    }

                    namespaces.Add(namespaceParent.Name.ToString());
                    namespaceParent = parentNamespace;
                }
            }

            return namespaces;
        }
    }
}
