using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class InternalsVisibleToAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        "AZC1200",
        "TypeExistsInAnotherAssembly 4",
        "{0} is defined in assembly {1} and is not publicly visible.",
        "Naming",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

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
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.LocalDeclarationStatement);
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InterfaceDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.Parameter);
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
            //else
            //{
            //    // your analysis code here
            //    var diagnostic = Diagnostic.Create(Rule, symbol.Locations[0], $"Sym sym:{context.Symbol.Kind} assm:{context.Compilation.Assembly.Identity.Name} {symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}");
            //    context.ReportDiagnostic(diagnostic);
            //}
            //else
            //{
            //    // your analysis code here
            //    var diagnostic = Diagnostic.Create(Rule, symbol.Locations[0], $"Sym sym:{context.Symbol.Kind} assm:{context.Compilation.Assembly.Identity.Name} {symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}");
            //    context.ReportDiagnostic(diagnostic);
            //}

            // Check if the symbol exists in another assembly and is not public
            //if (symbol.ContainingAssembly != null &&
            //    symbol.ContainingAssembly.Identity != context.Compilation.Assembly.Identity &&
            //    !symbol.IsPublic(context))
            //{
            //    var diagnostic = Diagnostic.Create(Rule, symbol.Locations[0], $"If sym:{context.Symbol.Kind} assm:{context.Compilation.Assembly.Identity.Name} {symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}");
            //    context.ReportDiagnostic(diagnostic);
            //}
            //else
            //{
            //    var diagnostic = Diagnostic.Create(Rule, symbol.Locations[0], $"Else sym:{context.Symbol.Kind} assm:{context.Compilation.Assembly.Identity.Name} {symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}");
            //    context.ReportDiagnostic(diagnostic);

            //}
        }

        private static void AnalysisParameter(IParameterSymbol parameterSymbol, SymbolAnalysisContext context)
        {
            var parentSymbol = context.Symbol;
        }

        private static void AnalysisField(IFieldSymbol symbol, SymbolAnalysisContext context)
        {
            var parentSymbol = context.Symbol;
            if (symbol.ContainingAssembly != null &&
                    symbol.Type.ContainingAssembly.Identity != context.Compilation.Assembly.Identity &&
                    !symbol.Type.IsPublic())
            {
                var diagnostic = Diagnostic.Create(Rule, parentSymbol.Locations[0], $"Field {symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} has a type which", symbol.Type.ContainingAssembly.Identity.Name);
                context.ReportDiagnostic(diagnostic);
            }

        }

        private static void AnalysisProperty(IPropertySymbol symbol, SymbolAnalysisContext context)
        {
            var parentSymbol = context.Symbol;
            if (symbol.ContainingAssembly != null &&
                    symbol.Type.ContainingAssembly.Identity != context.Compilation.Assembly.Identity &&
                    !symbol.Type.IsPublic())
            {
                var diagnostic = Diagnostic.Create(Rule, parentSymbol.Locations[0], $"Property with type {symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} has a type which", symbol.Type.ContainingAssembly.Identity.Name);
                context.ReportDiagnostic(diagnostic);
            }

        }

        private static void AnalysisMethod(IMethodSymbol symbol, SymbolAnalysisContext context)
        {
            var parentSymbol = context.Symbol;
            if (symbol.ContainingAssembly != null &&
                    symbol.ReturnType.ContainingAssembly.Identity != context.Compilation.Assembly.Identity &&
                    !symbol.ReturnType.IsPublic())
            {
                var diagnostic = Diagnostic.Create(Rule, parentSymbol.Locations[0], $"Method {symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} returns a type which", symbol.ReturnType.ContainingAssembly.Identity.Name);
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
                    if (interfaceSymbol.ContainingAssembly != null &&
                        interfaceSymbol.ContainingAssembly.Identity != context.Compilation.Assembly.Identity &&
                        !interfaceSymbol.IsPublic())
                    {
                        var diagnostic = Diagnostic.Create(Rule, parentSymbol.Locations[0], $"Type {context.Symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} implements interface {interfaceSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} which", interfaceSymbol.ContainingAssembly.Identity.Name);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
            if (symbol.BaseType != null)
            {
                if (symbol.BaseType.ContainingAssembly != null &&
                    symbol.BaseType.ContainingAssembly.Identity != context.Compilation.Assembly.Identity &&
                    !symbol.BaseType.IsPublic())
                {
                    var diagnostic = Diagnostic.Create(Rule, parentSymbol.Locations[0], $"Type {context.Symbol.Name} derives from base type {symbol.BaseType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} which", symbol.BaseType.ContainingAssembly.Identity.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        public void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is ParameterSyntax parameterSyntax)
            {
                AnalyzeParameter(parameterSyntax, context);
            }
        }

        private void AnalyzeInterfaceDeclaration(InterfaceDeclarationSyntax interfaceDeclarationSyntax, SyntaxNodeAnalysisContext context)
        {
            throw new NotImplementedException();
        }

        private void AnalyzeLocalDeclaration(LocalDeclarationStatementSyntax localDeclarationStatementSyntax, SyntaxNodeAnalysisContext context)
        {
            throw new NotImplementedException();
        }

        private void AnalyzeParameter(ParameterSyntax parameterSyntax, SyntaxNodeAnalysisContext context)
        {
            var typeInfo = context.SemanticModel.GetTypeInfo(parameterSyntax.Type);
            var containingAssembly = typeInfo.Type.ContainingAssembly;
            if (containingAssembly != null &&
                containingAssembly.Identity != context.Compilation.Assembly.Identity &&
                !typeInfo.Type.IsPublic())
            {
                var diagnostic = Diagnostic.Create(Rule, parameterSyntax.GetLocation(), $"Parameter {parameterSyntax.Identifier.ValueText} of type {typeInfo.Type.Name}", typeInfo.Type.ContainingAssembly);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    // Extension method for checking IsPublic
    public static class SymbolExtensions
    {
        public static bool IsPublic(this ISymbol symbol)
        {
            return symbol.DeclaredAccessibility == Accessibility.Public ||
                   symbol.DeclaredAccessibility == Accessibility.ProtectedOrInternal ||
                   symbol.DeclaredAccessibility == Accessibility.Protected;
        }

        private static bool IsInternalsVisibleTo(this IAssemblySymbol sourceAssembly, IAssemblySymbol targetAssembly)
        {
            var metadata = sourceAssembly.GetMetadata();
            var peref = metadata.GetReference();
            var path = peref.FilePath;
            return false;
            //return sourceAssembly.GetAttributes()
            //    .Where(a => a.AttributeClass.Name == "InternalsVisibleToAttribute")
            //    .Select(a => MetadataReference.CreateFromAssemblyAttribute(a))
            //    .Any(r => SymbolEqualityComparer.Default.Equals(targetAssembly, r.Resolve(targetAssembly, ignoreAssemblyKey: true)));
        }
    }
}
