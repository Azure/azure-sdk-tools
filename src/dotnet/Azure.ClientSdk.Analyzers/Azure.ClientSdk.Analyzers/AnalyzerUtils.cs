// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Azure.ClientSdk.Analyzers
{
    internal class AnalyzerUtils
    {
        internal static bool IsNotSdkCode(ISymbol symbol) => !IsSdkCode(symbol);

        internal static bool IsSdkCode(ISymbol symbol)
        {
            var ns = symbol.ContainingNamespace.GetFullNamespaceName();

            return IsSdkNamespace(ns);
        }

        internal static bool IsNotSdkCode(SyntaxNode node, SemanticModel model) => !IsSdkCode(node, model);

        internal static bool IsSdkCode(SyntaxNode node, SemanticModel model)
        {
            var symbol = model.GetDeclaredSymbol(node);
            if (symbol != null)
            {
                return IsSdkCode(symbol);
            }

            var ns = GetNamespace(node);
            return IsSdkNamespace(ns);
        }

        private static bool IsSdkNamespace(string ns)
        {
            var namespaces = ns.AsSpan();
            // if the namespace contains only one level, it's not SDK namespace
            var indexOfFirstDot = namespaces.IndexOf('.');
            if (indexOfFirstDot == -1)
                return false;

            // first namespace must be `Azure`
            var firstNamespace = namespaces.Slice(0, indexOfFirstDot);
            if (!firstNamespace.Equals("Azure".AsSpan(), StringComparison.Ordinal))
                return false;

            // second namespace must not be `Core`
            var remainingNamespace = namespaces.Slice(indexOfFirstDot + 1);
            var indexOfSecondDot = remainingNamespace.IndexOf('.');
            var seondNamespace = (indexOfSecondDot == -1 ? remainingNamespace : remainingNamespace.Slice(0, indexOfSecondDot));
            return !seondNamespace.Equals("Core".AsSpan(), StringComparison.Ordinal);
        }

        private static string GetNamespace(SyntaxNode node)
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


            return string.Join(".", namespaces.Reverse<string>());
        }
    }
}
