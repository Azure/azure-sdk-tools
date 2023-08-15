// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
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
        private const string ResponseTypeName = "Response";
        private const string NullableResponseTypeName = "NullableResponse";
        private const string OperationTypeName = "Operation";
        private const string TaskTypeName = "Task";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(new[]
        {
            Descriptors.AZC0002,
            Descriptors.AZC0003,
            Descriptors.AZC0004,
            Descriptors.AZC0015
        });

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

        private static void CheckCancellationTokenOrRequestContext(ISymbolAnalysisContext context, IMethodSymbol member)
        {
            var lastArgument = member.Parameters.LastOrDefault();
            var isCancellationOrRequestContext = lastArgument != null && IsCancellationOrRequestContext(lastArgument);

            if (!isCancellationOrRequestContext)
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
            }
        }

        private static void CheckClientMethod(ISymbolAnalysisContext context, IMethodSymbol member)
        {
            CheckClientMethodReturnType(context, member);

            if (!member.IsVirtual && !member.IsOverride)
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0003, member.Locations.First()), member);
            }
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

        private static IMethodSymbol FindSyncPeer(INamedTypeSymbol type, IMethodSymbol method)
        {
            IMethodSymbol syncMember = null;
            var syncMemberName = method.Name.Substring(0, method.Name.Length - AsyncSuffix.Length);
            syncMember = FindMethod(type.GetMembers(syncMemberName).OfType<IMethodSymbol>(), method.TypeParameters, method.Parameters);
            if (syncMember == null)
            {
                var lastArgument = method.Parameters.LastOrDefault();
                if (lastArgument != null && IsCancellationToken(lastArgument))
                {
                    syncMember = FindMethod(type.GetMembers(syncMemberName).OfType<IMethodSymbol>(), method.TypeParameters, method.Parameters.RemoveAt(method.Parameters.Length - 1), p => IsCancellationToken(p));
                }
            }

            return syncMember;
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

                    var syncMember = FindSyncPeer(type, methodSymbol);
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
                    CheckCancellationTokenOrRequestContext(context, methodSymbol);
                }
            }
        }
    }
}
