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

        private const string PageableTypeName = "Pageable";
        private const string AsyncPageableTypeName = "AsyncPageable";
        private const string BinaryDataTypeName = "BinaryData";
        private const string ResponseTypeName = "Response";
        private const string NullableResponseTypeName = "NullableResponse";
        private const string OperationTypeName = "Operation";
        private const string TaskTypeName = "Task";

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
            return parameterSymbol.Name == "cancellationToken" && parameterSymbol.Type.Name == "CancellationToken" && parameterSymbol.IsOptional;
        }

        private static void CheckClientMethod(ISymbolAnalysisContext context, IMethodSymbol member)
        {
            static bool IsCancellationOrRequestContext(IParameterSymbol parameterSymbol)
            {
                return IsCancellationToken(parameterSymbol) || IsRequestContext(parameterSymbol);
            }

            CheckClientMethodReturnType(context, member);

            if (!member.IsVirtual && !member.IsOverride)
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0003, member.Locations.First()), member);
            }

            var lastArgument = member.Parameters.LastOrDefault();
            var isCancellationOrRequestContext = lastArgument != null && IsCancellationOrRequestContext(lastArgument);

            if (!isCancellationOrRequestContext)
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0002, member.Locations.FirstOrDefault()), member);
            }
            else if (IsCancellationToken(lastArgument))
            {
                // A convenience method should not have RequestContent as parameter
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

        // A protocol method should not have model as type. Accepted return type: Response, Task<Response>, Pageable<BinaryData>, AsyncPageable<BinaryData>, Operation<BinaryData>, Task<Operation<BinaryData>>, Operation, Task<Operation>, Operation<Pageable<BinaryData>>, Task<Operation<AsyncPageable<BinaryData>>>
        private static void CheckProtocolMethodReturnType(ISymbolAnalysisContext context, IMethodSymbol method)
        {
            bool IsValidPageable(ITypeSymbol typeSymbol)
            {
                if ((!IsOrImplements(typeSymbol, PageableTypeName)) && (!IsOrImplements(typeSymbol, AsyncPageableTypeName)))
                {
                    return false;
                }

                var pageableTypeSymbol = typeSymbol as INamedTypeSymbol;
                if (!pageableTypeSymbol.IsGenericType)
                {
                    return false;
                }

                var pageableReturn = pageableTypeSymbol.TypeArguments.Single();
                if (!IsOrImplements(pageableReturn, BinaryDataTypeName))
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

            if (IsOrImplements(unwrappedType, ResponseTypeName))
            {
                if (unwrappedType is INamedTypeSymbol responseTypeSymbol && responseTypeSymbol.IsGenericType)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0018, method.Locations.FirstOrDefault()), method);
                }
                return;
            }
            else if (IsOrImplements(unwrappedType, OperationTypeName))
            {
                if (unwrappedType is INamedTypeSymbol operationTypeSymbol && operationTypeSymbol.IsGenericType)
                {
                    var operationReturn = operationTypeSymbol.TypeArguments.Single();
                    if (IsOrImplements(operationReturn, PageableTypeName) || IsOrImplements(operationReturn, AsyncPageableTypeName))
                    {
                        if (!IsValidPageable(operationReturn))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0018, method.Locations.FirstOrDefault()), method);
                        }
                        return;
                    }

                    if (!IsOrImplements(operationReturn, BinaryDataTypeName))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0018, method.Locations.FirstOrDefault()), method);
                    }
                }
                return;
            }
            else if (IsOrImplements(originalType, PageableTypeName) || IsOrImplements(originalType, AsyncPageableTypeName))
            { 
                if (!IsValidPageable(originalType))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0018, method.Locations.FirstOrDefault()), method);
                }
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0018, method.Locations.FirstOrDefault()), method);
        }

        private static bool IsOrImplements(ITypeSymbol typeSymbol, string typeName)
        {
            if (typeSymbol.Name == typeName)
            {
                return true;
            }

            if (typeSymbol.BaseType != null)
            {
                return IsOrImplements(typeSymbol.BaseType, typeName);
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

            if (IsOrImplements(unwrappedType, ResponseTypeName) ||
                IsOrImplements(unwrappedType, NullableResponseTypeName) ||
                IsOrImplements(unwrappedType, OperationTypeName) ||
                IsOrImplements(originalType, PageableTypeName) ||
                IsOrImplements(originalType, AsyncPageableTypeName))
            {
                return true;
            }

            if (throwError)
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0015, method.Locations.FirstOrDefault(), originalType.ToDisplayString()), method);
            }
            return false;
        }

        private bool IsCheckExempt(ISymbolAnalysisContext context, IMethodSymbol method)
        {
            return method.MethodKind != MethodKind.Ordinary ||
                method.DeclaredAccessibility != Accessibility.Public ||
                method.OverriddenMethod != null ||
                method.IsImplicitlyDeclared ||
                !IsClientMethodReturnType(context, method);
        }
        
        public override void AnalyzeCore(ISymbolAnalysisContext context)
        {
            INamedTypeSymbol type = (INamedTypeSymbol)context.Symbol;
            foreach (var member in type.GetMembers())
            {
                if (member is IMethodSymbol asyncMethodSymbol && !IsCheckExempt(context, asyncMethodSymbol) && asyncMethodSymbol.Name.EndsWith(AsyncSuffix))
                {
                    var syncMemberName = member.Name.Substring(0, member.Name.Length - AsyncSuffix.Length);
                    var syncMember = FindMethod(type.GetMembers(syncMemberName).OfType<IMethodSymbol>(), asyncMethodSymbol.TypeParameters, asyncMethodSymbol.Parameters);

                    if (syncMember == null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0004, member.Locations.First()), member);
                    }

                    CheckClientMethod(context, asyncMethodSymbol);
                }
                else if (member is IMethodSymbol syncMethodSymbol && !IsCheckExempt(context, syncMethodSymbol) && !syncMethodSymbol.Name.EndsWith(AsyncSuffix))
                {
                    var asyncMemberName = member.Name + AsyncSuffix;
                    var asyncMember = FindMethod(type.GetMembers(asyncMemberName).OfType<IMethodSymbol>(), syncMethodSymbol.TypeParameters, syncMethodSymbol.Parameters);

                    if (asyncMember == null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0004, member.Locations.First()), member);
                    }

                    CheckClientMethod(context, syncMethodSymbol);
                }
            }
        }
    }
}
