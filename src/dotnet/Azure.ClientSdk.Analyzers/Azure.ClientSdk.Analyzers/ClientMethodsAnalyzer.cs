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

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(new[]
        {
            Descriptors.AZC0002,
            Descriptors.AZC0003,
            Descriptors.AZC0004,
            Descriptors.AZC0015,
            Descriptors.AZC0017,
            Descriptors.AZC0018
        });

        private static void CheckClientMethod(ISymbolAnalysisContext context, IMethodSymbol member)
        {
            static bool SupportsCancellationsParameter(IParameterSymbol parameterSymbol)
            {
                return IsCancellationToken(parameterSymbol) || IsRequestContext(parameterSymbol);
            }

            static bool IsRequestContext(IParameterSymbol parameterSymbol)
            {
                return parameterSymbol.Name == "context" && parameterSymbol.Type.Name == "RequestContext";
            }

            static bool IsCancellationToken(IParameterSymbol parameterSymbol)
            {
                return parameterSymbol.Name == "cancellationToken" && parameterSymbol.Type.Name == "CancellationToken";
            }

            CheckClientMethodReturnType(context, member);

            if (!member.IsVirtual && !member.IsOverride)
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0003, member.Locations.First()), member);
            }

            var lastArgument = member.Parameters.LastOrDefault();
            var supportsCancellations = lastArgument != null && SupportsCancellationsParameter(lastArgument);

            if (!supportsCancellations)
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0002, member.Locations.FirstOrDefault()), member);
            }
            else if (IsCancellationToken(lastArgument))
            {
                // A convenience method should have optional CancellationToken
                if (!lastArgument.IsOptional)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0017, member.Locations.FirstOrDefault()), member);
                }

                // A convenience method should not have RequestContent as parameter
                var requestContent = member.Parameters.FirstOrDefault(parameter => parameter.Type.Name == "RequestContent");
                if (requestContent != null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0017, member.Locations.FirstOrDefault()), member);
                }
            }
            else if (IsRequestContext(lastArgument))
            {
                // A protocol method should not have model as type
                ITypeSymbol unwrappedType = member.ReturnType;

                if (member.ReturnType is INamedTypeSymbol namedTypeSymbol &&
                    namedTypeSymbol.IsGenericType &&
                    namedTypeSymbol.Name == "Task")
                {
                    unwrappedType = namedTypeSymbol.TypeArguments.Single();
                }

                if (unwrappedType.Name == "Response" && unwrappedType is INamedTypeSymbol responseTypeSymbol && responseTypeSymbol.IsGenericType)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0018, member.Locations.FirstOrDefault()), member);
                }
            }
        }

        private static void CheckClientMethodReturnType(ISymbolAnalysisContext context, IMethodSymbol method)
        {
            bool IsOrImplements(ITypeSymbol typeSymbol, string typeName)
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

            ITypeSymbol originalType = method.ReturnType;
            ITypeSymbol unwrappedType = method.ReturnType;

            if (method.ReturnType is INamedTypeSymbol namedTypeSymbol &&
                namedTypeSymbol.IsGenericType &&
                namedTypeSymbol.Name == "Task")
            {
                unwrappedType = namedTypeSymbol.TypeArguments.Single();
            }

            if (IsOrImplements(unwrappedType, "Response") ||
                IsOrImplements(unwrappedType, "NullableResponse") ||
                IsOrImplements(unwrappedType, "Operation") ||
                IsOrImplements(originalType, "Pageable") ||
                IsOrImplements(originalType, "AsyncPageable") ||
                originalType.Name.EndsWith(ClientSuffix))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0015, method.Locations.FirstOrDefault(), originalType.ToDisplayString()), method);

        }

        public override void AnalyzeCore(ISymbolAnalysisContext context)
        {
            INamedTypeSymbol type = (INamedTypeSymbol)context.Symbol;
            List<IMethodSymbol> visitedSyncMember = new List<IMethodSymbol>();
            foreach (var member in type.GetMembers())
            {
                if (member is IMethodSymbol asyncMethodSymbol && asyncMethodSymbol.Name.EndsWith(AsyncSuffix) && member.DeclaredAccessibility == Accessibility.Public)
                {
                    CheckClientMethod(context, asyncMethodSymbol);

                    var syncMemberName = member.Name.Substring(0, member.Name.Length - AsyncSuffix.Length);

                    var syncMember = FindMethod(type.GetMembers(syncMemberName).OfType<IMethodSymbol>(), asyncMethodSymbol.TypeParameters, asyncMethodSymbol.Parameters);

                    if (syncMember == null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0004, member.Locations.First()), member);
                    }
                    else
                    {
                        visitedSyncMember.Add(syncMember);
                        CheckClientMethod(context, syncMember);
                    }
                }
                else if (member is IMethodSymbol syncMethodSymbol && !member.IsImplicitlyDeclared && !syncMethodSymbol.Name.EndsWith(AsyncSuffix) && member.DeclaredAccessibility == Accessibility.Public && !visitedSyncMember.Contains(syncMethodSymbol))
                {
                    var asyncMemberName = member.Name + AsyncSuffix;
                    var asyncMember = FindMethod(type.GetMembers(asyncMemberName).OfType<IMethodSymbol>(), syncMethodSymbol.TypeParameters, syncMethodSymbol.Parameters);
                    if (asyncMember == null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0004, member.Locations.First()), member);
                    }
                }
            }
        }
    }
}
