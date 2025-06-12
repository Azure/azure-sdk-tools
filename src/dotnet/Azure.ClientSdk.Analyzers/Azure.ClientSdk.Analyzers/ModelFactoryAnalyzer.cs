// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ModelFactoryAnalyzer : DiagnosticAnalyzer
    {
        private const string ClientSuffix = "Client";
        private const string ModelFactorySuffix = "ModelFactory";

        private const string AzureNamespace = "Azure";
        private const string PageableTypeName = "Pageable";
        private const string AsyncPageableTypeName = "AsyncPageable";
        private const string ResponseTypeName = "Response";
        private const string NullableResponseTypeName = "NullableResponse";
        private const string OperationTypeName = "Operation";
        private const string TaskTypeName = "Task";
        private const string ClientResultTypeName = "ClientResult";
        private const string CollectionResultTypeName = "CollectionResult";
        private const string AsyncCollectionResultTypeName = "AsyncCollectionResult";
        private const string PageableOperationTypeName = "PageableOperation";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
            Descriptors.AZC0035
        );

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();
            context.RegisterCompilationAction(AnalyzeCompilation);
        }

        private static void AnalyzeCompilation(CompilationAnalysisContext context)
        {
            var outputModels = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            var modelFactoryMethods = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

            // Find all client classes and extract output models from their methods
            foreach (var namedType in GetAllTypes(context.Compilation.GlobalNamespace))
            {
                if (IsClientType(namedType))
                {
                    ExtractOutputModelsFromClientType(namedType, outputModels);
                }
                else if (IsModelFactoryType(namedType))
                {
                    ExtractReturnTypesFromModelFactory(namedType, modelFactoryMethods);
                }
            }

            // Check if all output models have corresponding model factory methods
            foreach (var outputModel in outputModels)
            {
                if (!modelFactoryMethods.Contains(outputModel))
                {
                    // Find a location to report the diagnostic - use the first location of the type
                    var location = outputModel.Locations.FirstOrDefault();
                    if (location != null)
                    {
                        var diagnostic = Diagnostic.Create(
                            Descriptors.AZC0035,
                            location,
                            outputModel.Name);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private static bool IsClientType(INamedTypeSymbol namedType)
        {
            return namedType.TypeKind == TypeKind.Class &&
                   namedType.Name.EndsWith(ClientSuffix) &&
                   namedType.DeclaredAccessibility == Accessibility.Public;
        }

        private static bool IsModelFactoryType(INamedTypeSymbol namedType)
        {
            return namedType.TypeKind == TypeKind.Class &&
                   namedType.Name.EndsWith(ModelFactorySuffix) &&
                   namedType.IsStatic &&
                   namedType.DeclaredAccessibility == Accessibility.Public;
        }

        private static void ExtractOutputModelsFromClientType(INamedTypeSymbol clientType, HashSet<ITypeSymbol> outputModels)
        {
            foreach (var member in clientType.GetMembers())
            {
                if (member is IMethodSymbol method && method.DeclaredAccessibility == Accessibility.Public)
                {
                    // Skip property accessors as they are not client methods
                    if (method.AssociatedSymbol is IPropertySymbol)
                        continue;

                    var outputModel = ExtractOutputModelFromReturnType(method.ReturnType);
                    if (outputModel != null)
                    {
                        outputModels.Add(outputModel);
                    }
                }
            }
        }

        private static void ExtractReturnTypesFromModelFactory(INamedTypeSymbol factoryType, HashSet<ITypeSymbol> modelFactoryMethods)
        {
            foreach (var member in factoryType.GetMembers())
            {
                if (member is IMethodSymbol method && 
                    method.DeclaredAccessibility == Accessibility.Public && 
                    method.IsStatic)
                {
                    // Add the return type of the factory method
                    modelFactoryMethods.Add(method.ReturnType);
                }
            }
        }

        private static ITypeSymbol ExtractOutputModelFromReturnType(ITypeSymbol returnType)
        {
            ITypeSymbol unwrappedType = returnType;

            // Unwrap Task<T>
            if (returnType is INamedTypeSymbol namedType && 
                namedType.IsGenericType && 
                namedType.Name == TaskTypeName)
            {
                unwrappedType = namedType.TypeArguments.FirstOrDefault();
                if (unwrappedType == null) return null;
            }

            ITypeSymbol modelType = null;

            // Check for Azure client method return types and extract the model type
            if (IsOrImplements(unwrappedType, ResponseTypeName, AzureNamespace) ||
                IsOrImplements(unwrappedType, NullableResponseTypeName, AzureNamespace) ||
                IsOrImplements(unwrappedType, ClientResultTypeName, AzureNamespace))
            {
                if (unwrappedType is INamedTypeSymbol responseType && responseType.IsGenericType)
                {
                    modelType = responseType.TypeArguments.FirstOrDefault();
                }
            }
            else if (IsOrImplements(unwrappedType, OperationTypeName, AzureNamespace))
            {
                if (unwrappedType is INamedTypeSymbol operationType && operationType.IsGenericType)
                {
                    modelType = operationType.TypeArguments.FirstOrDefault();
                }
            }
            else if (IsOrImplements(unwrappedType, PageableTypeName, AzureNamespace) ||
                     IsOrImplements(unwrappedType, AsyncPageableTypeName, AzureNamespace) ||
                     IsOrImplements(unwrappedType, CollectionResultTypeName, AzureNamespace) ||
                     IsOrImplements(unwrappedType, AsyncCollectionResultTypeName, AzureNamespace) ||
                     IsOrImplements(unwrappedType, PageableOperationTypeName, AzureNamespace))
            {
                if (unwrappedType is INamedTypeSymbol pageableType && pageableType.IsGenericType)
                {
                    modelType = pageableType.TypeArguments.FirstOrDefault();
                }
            }

            // Only return user-defined types, not built-in types
            if (modelType != null && IsUserDefinedModelType(modelType))
            {
                return modelType;
            }

            return null;
        }

        private static bool IsUserDefinedModelType(ITypeSymbol typeSymbol)
        {
            // Filter out built-in types
            if (typeSymbol.SpecialType != SpecialType.None)
            {
                return false;
            }

            // Only consider class types - all types in the assembly are candidates
            return typeSymbol.TypeKind == TypeKind.Class;
        }

        private static bool IsOrImplements(ITypeSymbol typeSymbol, string typeName, string namespaceName)
        {
            if (typeSymbol.Name == typeName && 
                typeSymbol.ContainingNamespace.Name == namespaceName && 
                typeSymbol.ContainingNamespace.ContainingNamespace.Name == "")
            {
                return true;
            }

            if (typeSymbol.BaseType != null)
            {
                return IsOrImplements(typeSymbol.BaseType, typeName, namespaceName);
            }

            return false;
        }

        private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol namespaceSymbol)
        {
            foreach (var type in namespaceSymbol.GetTypeMembers())
            {
                yield return type;
            }

            foreach (var childNamespace in namespaceSymbol.GetNamespaceMembers())
            {
                foreach (var type in GetAllTypes(childNamespace))
                {
                    yield return type;
                }
            }
        }
    }
}