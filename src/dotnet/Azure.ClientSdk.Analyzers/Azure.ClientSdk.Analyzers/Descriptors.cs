// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;

namespace Azure.ClientSdk.Analyzers
{
    internal class Descriptors
    {
        private static readonly string AZC0001Title = "Use one of the following pre-approved namespace groups (https://azure.github.io/azure-sdk/registered_namespaces.html): " + string.Join(", ", ClientAssemblyNamespaceAnalyzer.AllowedNamespacePrefix);

        public static DiagnosticDescriptor AZC0001 = new DiagnosticDescriptor(
            nameof(AZC0001), AZC0001Title,
            "Namespace '{0}' shouldn't contain public types. " + AZC0001Title, "Usage", DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor AZC0002 = new DiagnosticDescriptor(
            nameof(AZC0002),
            "DO ensure all service methods, both asynchronous and synchronous, take an optional CancellationToken parameter called cancellationToken.",
            "Client method should have cancellationToken as the last optional parameter (both name and it being optional matters)",
            "Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: null,
            "https://azure.github.io/azure-sdk/dotnet_introduction.html#dotnet-service-methods-cancellation"
        );

        public static DiagnosticDescriptor AZC0003 = new DiagnosticDescriptor(
            nameof(AZC0003),
            "DO make service methods virtual.",
            "DO make service methods virtual.",
            "Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: null,
            "https://azure.github.io/azure-sdk/dotnet_introduction.html#dotnet-service-methods-virtual"
        );

        public static DiagnosticDescriptor AZC0004 = new DiagnosticDescriptor(
            nameof(AZC0004),
            "DO provide both asynchronous and synchronous variants for all service methods.",
            "DO provide both asynchronous and synchronous variants for all service methods.",
            "Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: null,
            "https://azure.github.io/azure-sdk/dotnet_introduction.html#dotnet-service-methods-sync-and-async"
        );

        public static DiagnosticDescriptor AZC0005 = new DiagnosticDescriptor(
            nameof(AZC0005),
            "DO provide protected parameterless constructor for mocking.",
            "DO provide protected parameterless constructor for mocking.",
            "Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: null,
            "https://azure.github.io/azure-sdk/dotnet_introduction.html#dotnet-client-constructor-for-mocking"
        );

        public static DiagnosticDescriptor AZC0006 = new DiagnosticDescriptor(
            nameof(AZC0006),
            "DO provide constructor overloads that allow specifying additional options.",
            "Client type should have public constructor with equivalent parameters taking a ClientOptions type as last argument",
            "Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: null,
            "https://azure.github.io/azure-sdk/dotnet_introduction.html#dotnet-client-constructor-overloads"
        );

        public static DiagnosticDescriptor AZC0007 = new DiagnosticDescriptor(
            nameof(AZC0007),
            "DO provide a minimal constructor that takes only the parameters required to connect to the service.",
            "Client type should have public constructor with equivalent parameters not taking ClientOptions type as last argument",
            "Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: null,
            "https://azure.github.io/azure-sdk/dotnet_introduction.html#dotnet-client-constructor-minimal"
        );

        public static DiagnosticDescriptor AZC0008 = new DiagnosticDescriptor(
            nameof(AZC0008), "ClientOptions should have a nested enum called ServiceVersion",
            "Client type should have a nested enum called ServiceVersion", "Usage", DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor AZC0009 = new DiagnosticDescriptor(
            nameof(AZC0009), "ClientOptions constructors should take a ServiceVersion as their first parameter",
            "ClientOptions constructors should take a ServiceVersion as their first parameter.  Default constructor should be overloaded to provide ServiceVersion.", "Usage", DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor AZC0010 = new DiagnosticDescriptor(
            nameof(AZC0010), "ClientOptions constructors should default ServiceVersion to latest supported service version",
            "ClientOptions constructors should default ServiceVersion to latest supported service version", "Usage", DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor AZC0011 = new DiagnosticDescriptor(
            nameof(AZC0011), "Avoid InternalsVisibleTo to non-test assemblies",
            "Internal visible to product libraries effectively become public API and have to be versioned appropriately", "Usage", DiagnosticSeverity.Warning, true);


        public static DiagnosticDescriptor AZC0100 = new DiagnosticDescriptor(
            nameof(AZC0100),
            "ConfigureAwait(false) must be used.",
            "ConfigureAwait(false) must be used.",
            "Usage",
            DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor AZC0101 = new DiagnosticDescriptor(
            nameof(AZC0101),
            "Use ConfigureAwait(false) instead of ConfigureAwait(true).",
            "Use ConfigureAwait(false) instead of ConfigureAwait(true).",
            "Usage",
            DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor AZC0102 = new DiagnosticDescriptor(
            nameof(AZC0102),
            "Do not use GetAwaiter().GetResult().",
            "Do not use GetAwaiter().GetResult(). Use the TaskExtensions.EnsureCompleted() extension method instead.",
            "Usage",
            DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor AZC0103 = new DiagnosticDescriptor(
            nameof(AZC0103),
            "Do not wait synchronously in asynchronous scope.",
            "Do not use {0} in asynchronous scope. Use await keyword instead.",
            "Usage",
            DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor AZC0104 = new DiagnosticDescriptor(
            nameof(AZC0104),
            "Use EnsureCompleted() directly on asynchronous method return value.",
            "Don't use {0}. Call EnsureCompleted() extension method directly on the return value of the asynchronous method that has 'bool async' parameter.",
            "Usage",
            DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor AZC0105 = new DiagnosticDescriptor(
            nameof(AZC0105),
            "DO NOT add 'async' parameter to public methods.",
            "DO provide both asynchronous and synchronous variants for all service methods instead of one variant with 'async' parameter.",
            "Usage",
            DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor AZC0106 = new DiagnosticDescriptor(
            nameof(AZC0106),
            "Non-public asynchronous method needs 'async' parameter.",
            "Non-public asynchronous method that is called in synchronous scope should have a boolean 'async' parameter.",
            "Usage",
            DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor AZC0107 = new DiagnosticDescriptor(
            nameof(AZC0107),
            "DO NOT call public asynchronous method in synchronous scope.",
            "Public asynchronous method shouldn't be called in synchronous scope. Use synchronous version of the method if it is available.",
            "Usage",
            DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor AZC0108 = new DiagnosticDescriptor(
            nameof(AZC0108),
            "Incorrect 'async' parameter value.",
            "In {0} scope 'async' parameter for the '{1}' method call should {2}.",
            "Usage",
            DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor AZC0109 = new DiagnosticDescriptor(
            nameof(AZC0109),
            "Misuse of 'async' parameter.",
            "'async' parameter in asynchronous method can't be changed and can only be used as an exclusive condition in '?:' operator or conditional statement.",
            "Usage",
            DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor AZC0110 = new DiagnosticDescriptor(
            nameof(AZC0110),
            "DO NOT use await keyword in possibly synchronous scope.",
            "Asynchronous method with `async` parameter can be called from both synchronous and asynchronous scopes. 'await' keyword can be safely used either in guaranteed asynchronous scope (i.e. `if (async) {...}`) or if `async` parameter is passed into awaited method. Awaiting on variables, fields, properties, conditional operators or async methods that don't use `async` parameter isn't allowed outside of the guaranteed asynchronous scope.",
            "Usage",
            DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor AZC0111 = new DiagnosticDescriptor(
            nameof(AZC0111),
            "DO NOT use EnsureCompleted in possibly asynchronous scope.",
            "Asynchronous method with `async` parameter can be called from both synchronous and asynchronous scopes. 'EnsureCompleted' extension method can be safely used on in guaranteed synchronous scope (i.e. `if (!async) {...}`).",
            "Usage",
            DiagnosticSeverity.Warning, true);
    }
}
