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
            "Namespace '{0}' shouldn't contain public types. " + AZC0001Title, "Usage", DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor AZC0002 = new DiagnosticDescriptor(
            "AZC0002", "Client method should have cancellationToken as the last optional parameter",
            "Client method should have cancellationToken as the last optional parameter (both name and it being optional matters)", "Usage", DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor AZC0003 = new DiagnosticDescriptor(
            "AZC0003", "Client methods should be virtual",
            "Client methods should be virtual", "Usage", DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor AZC0004 = new DiagnosticDescriptor(
            "AZC0004", "Async client method should have sync alternative with same arguments",
            "Async client method should have sync alternative with same arguments", "Usage", DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor AZC0005 = new DiagnosticDescriptor(
            "AZC0005", "Client type should have protected parameterless constructor",
            "Client type should have protected parameterless constructor", "Usage", DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor AZC0006 = new DiagnosticDescriptor(
            "AZC0006", "Client type should have public constructor with equivalent parameters taking client options",
            "Client type should have public constructor with equivalent parameters taking '{0}' as last argument", "Usage", DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor AZC0007 = new DiagnosticDescriptor(
            "AZC0007", "Client type should have public constructor with equivalent parameters not taking client options",
            "Client type should have public constructor with equivalent parameters not taking '{0}' as last argument", "Usage", DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor AZC0008 = new DiagnosticDescriptor(
            "AZC0008", "ClientOptions should have a nested enum called ServiceVersion",
            "Client type should have a nested enum called ServiceVersion", "Usage", DiagnosticSeverity.Warning, true);
    }
}
