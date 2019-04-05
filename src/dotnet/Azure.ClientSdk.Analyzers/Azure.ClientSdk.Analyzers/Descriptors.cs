// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;

namespace Azure.ClientSdk.Analyzers
{
    internal class Descriptors
    {
        private static readonly string AZC0001Title = "Use one of the following pre-approved namespace groups: " + string.Join(", ", ClientAssemblyNamespaceAnalyzer.AllowedNamespacePrefix);

        public static DiagnosticDescriptor AZC0001 = new DiagnosticDescriptor(
            "AZC0001", AZC0001Title,
            "Namespace '{0}' shouldn't contain public types. " + AZC0001Title, "Usage", DiagnosticSeverity.Error, true);
    }
}
