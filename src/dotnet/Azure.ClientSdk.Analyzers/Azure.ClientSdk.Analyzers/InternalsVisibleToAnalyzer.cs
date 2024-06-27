using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class InternalsVisibleToAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptors.AZC0112);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();
            // Register for symbol actions
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Method);
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Property);
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Field);
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Parameter);
            context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var symbol = context.Symbol;
            if (symbol is INamedTypeSymbol namedTypeSymbol)
            {
                AnalysisNamedType(namedTypeSymbol, context);
            }
            else if (symbol is IMethodSymbol methodSymbol)
            {
                AnalysisMethod(methodSymbol, context);
            }
            else if (symbol is IPropertySymbol propertySymbol)
            {
                AnalysisProperty(propertySymbol, context);
            }
            else if (symbol is IFieldSymbol fieldSymbol)
            {
                AnalysisField(fieldSymbol, context);
            }
            else if (symbol is IParameterSymbol parameterSymbol)
            {
                AnalysisParameter(parameterSymbol, context);
            }
        }

        private static void AnalysisParameter(IParameterSymbol symbol, SymbolAnalysisContext context)
        {
            var parentSymbol = context.Symbol;
            if (symbol.Type.IsVisibleInternalWithoutFriendAttribute(context.Compilation))
            {
                var diagnostic = Diagnostic.Create(
                    Descriptors.AZC0112,
                    parentSymbol.Locations[0],
                    $"Parameter {symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} has a type which",
                    symbol.Type.ContainingAssembly.Identity.Name);
                context.ReportDiagnostic(diagnostic);
            }

        }

        private static void AnalysisField(IFieldSymbol symbol, SymbolAnalysisContext context)
        {
            var parentSymbol = context.Symbol;
            if (symbol.Type.IsVisibleInternalWithoutFriendAttribute(context.Compilation))
            {
                var diagnostic = Diagnostic.Create(
                    Descriptors.AZC0112,
                    parentSymbol.Locations[0],
                    $"Field {symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} has a type which",
                    symbol.Type.ContainingAssembly.Identity.Name);
                context.ReportDiagnostic(diagnostic);
            }

        }

        private static void AnalysisProperty(IPropertySymbol symbol, SymbolAnalysisContext context)
        {
            var parentSymbol = context.Symbol;
            if (symbol.Type.IsVisibleInternalWithoutFriendAttribute(context.Compilation))
            {
                var diagnostic = Diagnostic.Create(
                    Descriptors.AZC0112,
                    parentSymbol.Locations[0],
                    $"Property with type {symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} has a type which",
                    symbol.Type.ContainingAssembly.Identity.Name);
                context.ReportDiagnostic(diagnostic);
            }

        }

        private static void AnalysisMethod(IMethodSymbol symbol, SymbolAnalysisContext context)
        {
            var parentSymbol = context.Symbol;
            if (symbol.ReturnType.IsVisibleInternalWithoutFriendAttribute(context.Compilation))
            {
                var diagnostic = Diagnostic.Create(
                    Descriptors.AZC0112,
                    parentSymbol.Locations[0],
                    $"Method {symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} returns a type which",
                    symbol.ReturnType.ContainingAssembly.Identity.Name);
                context.ReportDiagnostic(diagnostic);
            }

        }

        private static void AnalysisNamedType(INamedTypeSymbol symbol, SymbolAnalysisContext context)
        {
            var parentSymbol = context.Symbol;
            if (symbol.Interfaces.Length > 0)
            {
                foreach (var interfaceSymbol in symbol.Interfaces)
                {
                    if (interfaceSymbol.IsVisibleInternalWithoutFriendAttribute(context.Compilation))
                    {
                        var diagnostic = Diagnostic.Create(
                            Descriptors.AZC0112,
                            parentSymbol.Locations[0],
                            $"Type {parentSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} implements interface {interfaceSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} which",
                            interfaceSymbol.ContainingAssembly.Identity.Name);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
            if (symbol.BaseType != null)
            {
                if (symbol.BaseType.IsVisibleInternalWithoutFriendAttribute(context.Compilation))
                {
                    var diagnostic = Diagnostic.Create(
                        Descriptors.AZC0112,
                        parentSymbol.Locations[0],
                        $"Type {parentSymbol.Name} derives from base type {symbol.BaseType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} which",
                        symbol.BaseType.ContainingAssembly.Identity.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
        {
            var memberAccess = (MemberAccessExpressionSyntax)context.Node;

            if (memberAccess.Expression is IdentifierNameSyntax identifierName && memberAccess.Name is IdentifierNameSyntax memberName)
            {
                var symbol = context.SemanticModel.GetSymbolInfo(memberName).Symbol;
                if (symbol is IPropertySymbol propertySymbol && propertySymbol.IsVisibleInternalWithoutFriendAttribute(context.Compilation))
                {
                        var diagnostic = Diagnostic.Create(
                            Descriptors.AZC0112,
                            memberAccess.GetLocation(),
                            $"Accessed property {propertySymbol.Name} has a type which",
                            propertySymbol.Type.ContainingAssembly.Identity.Name);
                        context.ReportDiagnostic(diagnostic);
                }
                else if(symbol is IMethodSymbol methodSymbol && methodSymbol.IsVisibleInternalWithoutFriendAttribute(context.Compilation))
                {
                    var diagnostic = Diagnostic.Create(
                        Descriptors.AZC0112,
                        memberAccess.GetLocation(),
                        $"Accessed method {methodSymbol.Name} has a return type which",
                        methodSymbol.ReturnType.ContainingAssembly.Identity.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    public static class SymbolExtensions
    {
        public static bool IsVisibleInternalWithoutFriendAttribute(this ISymbol symbol, Compilation contextCompilation) =>
            symbol.DeclaredAccessibility == Accessibility.Internal &&
            symbol.ContainingAssembly != null &&
            symbol.ContainingAssembly.Identity != contextCompilation.Assembly.Identity &&
            !symbol.GetAttributes().Any(ad => ad.AttributeClass.Name == "FriendAttribute");
    }
}
