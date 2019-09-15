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
            "AZC0002",
            "DO ensure all service methods, both asynchronous and synchronous, take an optional CancellationToken parameter called cancellationToken.",
            "Client method should have cancellationToken as the last optional parameter (both name and it being optional matters)",
            "Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: null,
            "https://azure.github.io/azure-sdk/dotnet_introduction.html#dotnet-service-methods-cancellation"
        );

        public static DiagnosticDescriptor AZC0003 = new DiagnosticDescriptor(
            "AZC0003",
            "DO make service methods virtual.",
            "DO make service methods virtual.",
            "Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: null,
            "https://azure.github.io/azure-sdk/dotnet_introduction.html#dotnet-service-methods-virtual"
        );

        public static DiagnosticDescriptor AZC0004 = new DiagnosticDescriptor(
            "AZC0004",
            "DO provide both asynchronous and synchronous variants for all service methods.",
            "DO provide both asynchronous and synchronous variants for all service methods.",
            "Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: null,
            "https://azure.github.io/azure-sdk/dotnet_introduction.html#dotnet-service-methods-sync-and-async"
        );

        public static DiagnosticDescriptor AZC0005 = new DiagnosticDescriptor(
            "AZC0005",
            "DO provide protected parameterless constructor for mocking.",
            "DO provide protected parameterless constructor for mocking.",
            "Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: null,
            "https://azure.github.io/azure-sdk/dotnet_introduction.html#dotnet-client-constructor-for-mocking"
        );

        public static DiagnosticDescriptor AZC0006 = new DiagnosticDescriptor(
            "AZC0006",
            "DO provide constructor overloads that allow specifying additional options.",
            "Client type should have public constructor with equivalent parameters taking '{0}' as last argument",
            "Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: null,
            "https://azure.github.io/azure-sdk/dotnet_introduction.html#dotnet-client-constructor-overloads"
        );

        public static DiagnosticDescriptor AZC0007 = new DiagnosticDescriptor(
            "AZC0007",
            "DO provide a minimal constructor that takes only the parameters required to connect to the service.",
            "Client type should have public constructor with equivalent parameters not taking '{0}' as last argument",
            "Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: null,
            "https://azure.github.io/azure-sdk/dotnet_introduction.html#dotnet-client-constructor-minimal"
        );

        public static DiagnosticDescriptor AZC0008 = new DiagnosticDescriptor(
            "AZC0008", "ClientOptions should have a nested enum called ServiceVersion",
            "Client type should have a nested enum called ServiceVersion", "Usage", DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor AZC0009 = new DiagnosticDescriptor(
            "AZC0009", "ClientOptions constructors should take a ServiceVersion as their first parameter",
            "ClientOptions constructors should take a ServiceVersion as their first parameter.  Default constructor should be overloaded to provide ServiceVersion.", "Usage", DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor AZC0010 = new DiagnosticDescriptor(
            "AZC0010", "ClientOptions constructors should default ServiceVersion to latest supported service version",
            "ClientOptions constructors should default ServiceVersion to latest supported service version", "Usage", DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor AZC1001 = new DiagnosticDescriptor(
            "AZC1001", "Avoid multiple newlines",
            "Avoid multiple newlines", "Usage", DiagnosticSeverity.Warning, true);
        public static DiagnosticDescriptor AZC1002 = new DiagnosticDescriptor(
            "AZC1002", "Avoid whitespace in the end of the line",
            "Avoid whitespace in the end of the line", "Usage", DiagnosticSeverity.Warning, true);
    }
}
