// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            base.Initialize(context);
            context.RegisterCodeBlockAction(c => AnalyzeCodeBlock(c));
        }

        private void AnalyzeCodeBlock(CodeBlockAnalysisContext codeBlock)
        {
            var symbol = codeBlock.OwningSymbol;
            if (symbol is IMethodSymbol methodSymbol)
            {
                var lastParameter = methodSymbol.Parameters.LastOrDefault();
                if (lastParameter != null && IsRequestContext(lastParameter))
                {
                    var requestContent = methodSymbol.Parameters.FirstOrDefault(p => IsRequestContent(p));
                    if (requestContent != null)
                    {
                        bool isRequired = ContainsAssertNotNull(codeBlock, requestContent.Name);
                        if (isRequired && !lastParameter.IsOptional)
                        {
                            codeBlock.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0018, symbol.Locations.FirstOrDefault()));
                        }
                        if (!isRequired && lastParameter.IsOptional)
                        {
                            codeBlock.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0018, symbol.Locations.FirstOrDefault()));
                        }
                    }
                }
            }
        }

        private static bool ContainsAssertNotNull(CodeBlockAnalysisContext codeBlock, string variableName)
        {
            // Check Argument.AssertNotNull(variableName, nameof(variableName));
            foreach (var invocation in codeBlock.CodeBlock.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is MemberAccessExpressionSyntax assertNotNull && assertNotNull.Name.Identifier.Text == "AssertNotNull")
                {
                    if (assertNotNull.Expression is IdentifierNameSyntax identifierName && identifierName.Identifier.Text == "Argument" ||
                        assertNotNull.Expression is MemberAccessExpressionSyntax memberAccessExpression && memberAccessExpression.Name.Identifier.Text == "Argument")
                    {
                        var argumentsList = invocation.ArgumentList.Arguments;
                        if (argumentsList.Count != 2)
                        {
                            continue;
                        }
                        if (argumentsList.First().Expression is IdentifierNameSyntax first)
                        {
                            if (first.Identifier.Text != variableName)
                            {
                                continue;
                            }
                            if (argumentsList.Last().Expression is InvocationExpressionSyntax second)
                            {
                                if (second.Expression is IdentifierNameSyntax nameof && nameof.Identifier.Text == "nameof")
                                {
                                    if (second.ArgumentList.Arguments.Count != 1)
                                    {
                                        continue;
                                    }
                                    if (second.ArgumentList.Arguments.First().Expression is IdentifierNameSyntax contentName && contentName.Identifier.Text == variableName)
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static bool IsRequestContent(IParameterSymbol parameterSymbol)
        {
            return parameterSymbol.Type.Name == "RequestContent";
        }

        private static bool IsRequestContext(IParameterSymbol parameterSymbol)
        {
            return parameterSymbol.Name == "context" && parameterSymbol.Type.Name == "RequestContext";
        }

        private static void CheckClientMethod(ISymbolAnalysisContext context, IMethodSymbol member)
        {
            static bool IsCancellationOrRequestContext(IParameterSymbol parameterSymbol)
            {
                return IsCancellationToken(parameterSymbol) || IsRequestContext(parameterSymbol);
            }

            static bool IsCancellationToken(IParameterSymbol parameterSymbol)
            {
                return parameterSymbol.Name == "cancellationToken" && parameterSymbol.Type.Name == "CancellationToken" && parameterSymbol.IsOptional;
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
                var requestContent = member.Parameters.FirstOrDefault(parameter => parameter.Type.Name == "RequestContent");
                if (requestContent != null)
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
        // Ambiguity: no RequestContent or has optional RequestContent.
        // No ambiguity: has required RequestContent.
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

            var requestContent = method.Parameters.FirstOrDefault(p => p.Type.Name == "RequestContent");
            if (requestContent == null)
            {
                if (method.Parameters.LastOrDefault().IsOptional)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0018, method.Locations.FirstOrDefault()), method);
                }
            }
            // Optional RequestContent or required RequestContent is checked in AnalyzeCodeBlock.
        }

        // A protocol method should not have model as type. Accepted return type: Response, Task<Response>, Pageable<BinaryData>, AsyncPageable<BinaryData>, Operation<BinaryData>, Task<Operation<BinaryData>>, Operation, Task<Operation>
        private static void CheckProtocolMethodReturnType(ISymbolAnalysisContext context, IMethodSymbol method)
        {
            ITypeSymbol originalType = method.ReturnType;
            ITypeSymbol unwrappedType = method.ReturnType;

            if (method.ReturnType is INamedTypeSymbol namedTypeSymbol &&
                namedTypeSymbol.IsGenericType &&
                namedTypeSymbol.Name == "Task")
            {
                unwrappedType = namedTypeSymbol.TypeArguments.Single();
            }

            if (unwrappedType.Name == "Response")
            {
                if (unwrappedType is INamedTypeSymbol responseTypeSymbol && responseTypeSymbol.IsGenericType)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0018, method.Locations.FirstOrDefault()), method);
                }
                return;
            }
            else if (unwrappedType.Name == "Operation")
            {
                if (unwrappedType is INamedTypeSymbol operationTypeSymbol && operationTypeSymbol.IsGenericType)
                {
                    var operationReturn = operationTypeSymbol.TypeArguments.Single();
                    if (operationReturn.Name != "BinaryData")
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0018, method.Locations.FirstOrDefault()), method);
                    }
                }
                return;
            }
            else if (originalType.Name == "Pageable" || originalType.Name == "AsyncPageable")
            {
                if (originalType is INamedTypeSymbol pageableTypeSymbol)
                {
                    if (!pageableTypeSymbol.IsGenericType)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0018, method.Locations.FirstOrDefault()), method);
                    }

                    var pageableReturn = pageableTypeSymbol.TypeArguments.Single();
                    if (pageableReturn.Name != "BinaryData")
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0018, method.Locations.FirstOrDefault()), method);
                    }
                }

                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0018, method.Locations.FirstOrDefault()), method);
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
