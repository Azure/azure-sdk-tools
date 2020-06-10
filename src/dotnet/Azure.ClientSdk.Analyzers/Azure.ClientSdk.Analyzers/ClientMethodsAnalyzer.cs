// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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
            Descriptors.AZC0015
        });

        private static void CheckClientMethod(ISymbolAnalysisContext context, IMethodSymbol member)
        {
            static bool IsCancellationTokenParameter(IParameterSymbol parameterSymbol)
            {
                return parameterSymbol.Name == "cancellationToken" && parameterSymbol.Type.Name == "CancellationToken";
            }

            CheckClientMethodReturnType(context, member);

            if (!member.IsVirtual && !member.IsOverride)
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0003, member.Locations.First()), member);
            }

            var lastArgument = member.Parameters.LastOrDefault();
            var isCancellationTokenParameter = lastArgument != null && IsCancellationTokenParameter(lastArgument);

            if (!isCancellationTokenParameter)
            {
                var overloadWithCancellationToken = FindMethod(
                    member.ContainingType.GetMembers(member.Name).OfType<IMethodSymbol>(),
                    member.TypeParameters,
                    member.Parameters,
                    p => IsCancellationTokenParameter(p));

                if (overloadWithCancellationToken != null)
                {
                    // Skip methods that have overloads with cancellation tokens
                    return;
                }

                context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0002, member.Locations.FirstOrDefault()), member);
            }
            else if (!lastArgument.IsOptional)
            {
                var overloadWithCancellationToken = FindMethod(
                    member.ContainingType.GetMembers(member.Name).OfType<IMethodSymbol>(),
                    member.TypeParameters,
                    member.Parameters.RemoveAt(member.Parameters.Length - 1));

                if (overloadWithCancellationToken != null)
                {
                    // Skip methods that have non-optional cancellation token if overload exists without one
                    return;
                }

                context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0002, member.Locations.FirstOrDefault()), member);
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
            foreach (var member in type.GetMembers())
            {
                if (member is IMethodSymbol methodSymbol && methodSymbol.Name.EndsWith(AsyncSuffix) && member.DeclaredAccessibility == Accessibility.Public)
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
            }
        }
    }
}