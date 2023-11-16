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
    public class ClientMethodsAnalyzer : ClientAnalyzerBase
    {
        private const string AsyncSuffix = "Async";

        private const string AzureNamespace = "Azure";
        private const string SystemNamespace = "System";
        private const string PageableTypeName = "Pageable";
        private const string AsyncPageableTypeName = "AsyncPageable";
        private const string BinaryDataTypeName = "BinaryData";
        private const string ResponseTypeName = "Response";
        private const string NullableResponseTypeName = "NullableResponse";
        private const string OperationTypeName = "Operation";
        private const string TaskTypeName = "Task";
        private const string BooleanTypeName = "Boolean";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(new[]
        {
            Descriptors.AZC0002,
            Descriptors.AZC0003,
            Descriptors.AZC0004,
            Descriptors.AZC0015,
            Descriptors.AZC0017,
            Descriptors.AZC0018,
            Descriptors.AZC0019,
        });

        private static bool IsRequestContent(IParameterSymbol parameterSymbol)
        {
            return parameterSymbol.Type.Name == "RequestContent";
        }

        private static bool IsRequestContext(IParameterSymbol parameterSymbol)
        {
            return parameterSymbol.Name == "context" && parameterSymbol.Type.Name == "RequestContext";
        }

        private static bool IsCancellationToken(IParameterSymbol parameterSymbol)
        {
            return parameterSymbol.Name == "cancellationToken" && parameterSymbol.Type.Name == "CancellationToken";
        }

        private static bool IsCancellationOrRequestContext(IParameterSymbol parameterSymbol)
        {
            return IsCancellationToken(parameterSymbol) || IsRequestContext(parameterSymbol);
        }

        private static void CheckServiceMethod(ISymbolAnalysisContext context, IMethodSymbol member)
        {
            var lastArgument = member.Parameters.LastOrDefault();
            var isLastArgumentCancellationOrRequestContext = lastArgument != null && IsCancellationOrRequestContext(lastArgument);

            if (!isLastArgumentCancellationOrRequestContext)
            {
                var overloadSupportsCancellations = FindMethod(
                    member.ContainingType.GetMembers(member.Name).OfType<IMethodSymbol>(),
                    member.TypeParameters,
                    member.Parameters,
                    p => IsCancellationToken(p));

                if (overloadSupportsCancellations == null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0002, member.Locations.FirstOrDefault()), member);
                }
            }
            else if (IsCancellationToken(lastArgument))
            {
                if (!lastArgument.IsOptional)
                {
                    var overloadWithCancellationToken = FindMethod(
                        member.ContainingType.GetMembers(member.Name).OfType<IMethodSymbol>(),
                        member.TypeParameters,
                        member.Parameters.RemoveAt(member.Parameters.Length - 1));

                    if (overloadWithCancellationToken == null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0002, member.Locations.FirstOrDefault()), member);
                    }
                }

                if (member.Parameters.FirstOrDefault(IsRequestContent) != null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0017, member.Locations.FirstOrDefault()), member);
                }
            }
            else if (IsRequestContext(lastArgument))
            {
                CheckProtocolMethodReturnType(context, member);
                CheckProtocolMethodParameters(context, member);
            }
        }

        private static string GetFullNamespaceName(IParameterSymbol parameter)
        {
            var currentNamespace = parameter.Type.ContainingNamespace;
            string currentName = currentNamespace.Name;
            string fullNamespace = "";
            while (!string.IsNullOrEmpty(currentName))
            {
                fullNamespace = fullNamespace == "" ? currentName : $"{currentName}.{fullNamespace}";
                currentNamespace = currentNamespace.ContainingNamespace;
                currentName = currentNamespace.Name;
            }
            return fullNamespace;
        }

        // A protocol method should not have model as parameter. If it has ambiguity with convenience method, it should have required RequestContext.
        // Ambiguity: doesn't have a RequestContent, but there is a method ending with CancellationToken has same type of parameters 
        // No ambiguity: has RequestContent.
        private static void CheckProtocolMethodParameters(ISymbolAnalysisContext context, IMethodSymbol method)
        {
            var containsModel = method.Parameters.Any(p =>
            {
                var fullNamespace = GetFullNamespaceName(p);
                return !fullNamespace.StartsWith("System") && !fullNamespace.StartsWith("Azure");
            });

            if (containsModel)
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0018, method.Locations.FirstOrDefault()), method);
                return;
            }

            var requestContent = method.Parameters.FirstOrDefault(IsRequestContent);
            if (requestContent == null && method.Parameters.Last().IsOptional)
            {
                INamedTypeSymbol type = (INamedTypeSymbol)context.Symbol;
                IEnumerable<IMethodSymbol> methodList = type.GetMembers(method.Name).OfType<IMethodSymbol>().Where(member => !SymbolEqualityComparer.Default.Equals(member, method));
                ImmutableArray<IParameterSymbol> parametersWithoutLast = method.Parameters.RemoveAt(method.Parameters.Length - 1);
                IMethodSymbol convenienceMethod = FindMethod(methodList, method.TypeParameters, parametersWithoutLast, symbol => IsCancellationToken(symbol));
                if (convenienceMethod != null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0019, method.Locations.FirstOrDefault()), method);
                }
            }
        }

        // A protocol method should not have model as type. Accepted return type: Response, Task<Response>, Response<bool>, Task<Response<bool>>, Pageable<BinaryData>, AsyncPageable<BinaryData>, Operation<BinaryData>, Task<Operation<BinaryData>>, Operation, Task<Operation>, Operation<Pageable<BinaryData>>, Task<Operation<AsyncPageable<BinaryData>>>
        private static void CheckProtocolMethodReturnType(ISymbolAnalysisContext context, IMethodSymbol method)
        {
            bool IsValidPageable(ITypeSymbol typeSymbol)
            {
                var pageableTypeSymbol = typeSymbol as INamedTypeSymbol;
                if (!pageableTypeSymbol.IsGenericType)
                {
                    return false;
                }

                var pageableReturn = pageableTypeSymbol.TypeArguments.Single();
                if (!IsOrImplements(pageableReturn, BinaryDataTypeName, SystemNamespace))
                {
                    return false;
                }

                return true;
            }

            ITypeSymbol originalType = method.ReturnType;
            ITypeSymbol unwrappedType = method.ReturnType;

            if (method.ReturnType is INamedTypeSymbol namedTypeSymbol &&
                namedTypeSymbol.IsGenericType &&
                namedTypeSymbol.Name == TaskTypeName)
            {
                unwrappedType = namedTypeSymbol.TypeArguments.Single();
            }

            if (IsOrImplements(unwrappedType, ResponseTypeName, AzureNamespace))
            {
                if (unwrappedType is INamedTypeSymbol responseTypeSymbol && responseTypeSymbol.IsGenericType)
                {
                    var responseReturn = responseTypeSymbol.TypeArguments.Single();
                    if (responseReturn.Name != BooleanTypeName)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0018, method.Locations.FirstOrDefault()), method);
                    }
                }
                return;
            }
            else if (IsOrImplements(unwrappedType, OperationTypeName, AzureNamespace))
            {
                if (unwrappedType is INamedTypeSymbol operationTypeSymbol && operationTypeSymbol.IsGenericType)
                {
                    var operationReturn = operationTypeSymbol.TypeArguments.Single();
                    if (IsOrImplements(operationReturn, PageableTypeName, AzureNamespace) || IsOrImplements(operationReturn, AsyncPageableTypeName, AzureNamespace))
                    {
                        if (!IsValidPageable(operationReturn))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0018, method.Locations.FirstOrDefault()), method);
                        }
                        return;
                    }

                    if (!IsOrImplements(operationReturn, BinaryDataTypeName, SystemNamespace))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0018, method.Locations.FirstOrDefault()), method);
                    }
                }
                return;
            }
            else if (IsOrImplements(originalType, PageableTypeName, AzureNamespace) || IsOrImplements(originalType, AsyncPageableTypeName, AzureNamespace))
            {
                if (!IsValidPageable(originalType))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0018, method.Locations.FirstOrDefault()), method);
                }
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0018, method.Locations.FirstOrDefault()), method);
        }

        private static void CheckClientMethod(ISymbolAnalysisContext context, IMethodSymbol member)
        {
            CheckClientMethodReturnType(context, member);

            if (!member.IsVirtual && !member.IsOverride)
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0003, member.Locations.First()), member);
            }
        }

        private static bool IsOrImplements(ITypeSymbol typeSymbol, string typeName, string namespaceName)
        {
            if (typeSymbol.Name == typeName && typeSymbol.ContainingNamespace.Name == namespaceName && typeSymbol.ContainingNamespace.ContainingNamespace.Name == "")
            {
                return true;
            }

            if (typeSymbol.BaseType != null)
            {
                return IsOrImplements(typeSymbol.BaseType, typeName, namespaceName);
            }

            return false;
        }

        private static void CheckClientMethodReturnType(ISymbolAnalysisContext context, IMethodSymbol method)
        {
            IsClientMethodReturnType(context, method, true);
        }

        private static bool IsClientMethodReturnType(ISymbolAnalysisContext context, IMethodSymbol method, bool throwError = false)
        {
            ITypeSymbol originalType = method.ReturnType;
            ITypeSymbol unwrappedType = method.ReturnType;

            if (method.ReturnType is INamedTypeSymbol namedTypeSymbol &&
                namedTypeSymbol.IsGenericType &&
                namedTypeSymbol.Name == TaskTypeName)
            {
                unwrappedType = namedTypeSymbol.TypeArguments.Single();
            }

            if (IsOrImplements(unwrappedType, ResponseTypeName, AzureNamespace) ||
                IsOrImplements(unwrappedType, NullableResponseTypeName, AzureNamespace) ||
                IsOrImplements(unwrappedType, OperationTypeName, AzureNamespace) ||
                IsOrImplements(originalType, PageableTypeName, AzureNamespace) ||
                IsOrImplements(originalType, AsyncPageableTypeName, AzureNamespace))
            {
                return true;
            }

            if (throwError)
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0015, method.Locations.FirstOrDefault(), originalType.ToDisplayString()), method);
            }
            return false;
        }

        public override void AnalyzeCore(ISymbolAnalysisContext context)
        {
            INamedTypeSymbol type = (INamedTypeSymbol)context.Symbol;
            foreach (var member in type.GetMembers())
            {
                var methodSymbol = member as IMethodSymbol;
                if (methodSymbol == null || methodSymbol.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                if (methodSymbol.Name.EndsWith(AsyncSuffix))
                {
                    CheckClientMethod(context, methodSymbol);

                    var syncMemberName = member.Name.Substring(0, member.Name.Length - AsyncSuffix.Length);

                    var syncMember = FindMethod(type.GetMembers(syncMemberName).OfType<IMethodSymbol>(), methodSymbol.TypeParameters, methodSymbol.Parameters);

                    if (syncMember == null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0004, member.Locations.First()), member);
                    }
                    else
                    {
                        CheckClientMethod(context, syncMember);
                    }
                }

                if (IsClientMethodReturnType(context, methodSymbol, false))
                {
                    CheckServiceMethod(context, methodSymbol);
                }
            }
        }
    }
}
