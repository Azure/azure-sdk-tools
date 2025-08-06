using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.Sdk.Tools.Cli.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EnforceToolsReturnTypesAnalyzer : DiagnosticAnalyzer
    {
        public const string Id = "MCP003";
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            Id,
            "Tool methods must return Response types, built-in value types, or string",
            "Method '{0}' in Tools namespace must return a class implementing Response, a built-in value type, or string. Current return type: '{1}'.",
            "Design",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;
            var semanticModel = context.SemanticModel;

            // Get method symbol
            var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration) as IMethodSymbol;
            if (methodSymbol == null)
                return;

            // Only analyze public non-static methods
            if (!methodSymbol.DeclaredAccessibility.HasFlag(Accessibility.Public) || methodSymbol.IsStatic)
                return;

            // Only analyze methods in classes within Azure.Sdk.Tools.Cli.Tools namespace
            var containingType = methodSymbol.ContainingType;
            if (containingType?.ContainingNamespace == null)
                return;

            var namespaceName = containingType.ContainingNamespace.ToDisplayString();
            if (!namespaceName.StartsWith("Azure.Sdk.Tools.Cli.Tools"))
                return;

            // Exclude abstract methods and virtual methods that are likely from base classes
            if (methodSymbol.IsAbstract || methodSymbol.IsVirtual || methodSymbol.IsOverride)
                return;

            // Exclude specific framework methods by name
            if (methodSymbol.Name == "GetCommand" || methodSymbol.Name == "HandleCommand")
            {
                return;
            }

            // Get the return type
            var returnType = methodSymbol.ReturnType;
            
            // Handle Task<T> and ValueTask<T> - get the inner type
            if (IsTaskType(returnType, out var innerType))
            {
                returnType = innerType;
            }

            // Check if return type is valid
            if (!IsValidReturnType(returnType, context.Compilation))
            {
                var returnTypeDisplayName = returnType.ToDisplayString();
                var diagnostic = Diagnostic.Create(Rule, 
                    methodDeclaration.Identifier.GetLocation(), 
                    methodSymbol.Name, 
                    returnTypeDisplayName);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static bool IsTaskType(ITypeSymbol type, out ITypeSymbol innerType)
        {
            innerType = type;

            if (type is INamedTypeSymbol namedType)
            {
                // Check for Task<T>
                if (namedType.IsGenericType && 
                    (namedType.ConstructedFrom?.ToDisplayString() == "System.Threading.Tasks.Task<T>" ||
                     namedType.Name == "Task" && namedType.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks"))
                {
                    if (namedType.TypeArguments.Length > 0)
                    {
                        innerType = namedType.TypeArguments[0];
                        return true;
                    }
                }

                // Check for ValueTask<T>
                if (namedType.IsGenericType && 
                    (namedType.ConstructedFrom?.ToDisplayString() == "System.Threading.Tasks.ValueTask<T>" ||
                     namedType.Name == "ValueTask" && namedType.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks"))
                {
                    if (namedType.TypeArguments.Length > 0)
                    {
                        innerType = namedType.TypeArguments[0];
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsValidReturnType(ITypeSymbol returnType, Compilation compilation)
        {
            // Built-in value types and string
            if (IsBuiltInValueTypeOrString(returnType))
                return true;

            // void is allowed (for non-async methods)
            if (returnType.SpecialType == SpecialType.System_Void)
                return true;

            // Task (without generic parameter) is allowed for void async methods
            if (returnType.Name == "Task" && 
                returnType.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks" &&
                returnType is INamedTypeSymbol namedTaskType && !namedTaskType.IsGenericType)
                return true;

            // Check if it implements Response (look in Azure.Sdk.Tools.Cli.Models namespace)
            var responseType = compilation.GetTypeByMetadataName("Azure.Sdk.Tools.Cli.Models.Response");
            if (responseType != null && InheritsFromOrImplements(returnType, responseType))
                return true;

            return false;
        }

        private static bool IsBuiltInValueTypeOrString(ITypeSymbol type)
        {
            switch (type.SpecialType)
            {
                case SpecialType.System_Boolean:
                case SpecialType.System_Char:
                case SpecialType.System_SByte:
                case SpecialType.System_Byte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Decimal:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_String:
                case SpecialType.System_IntPtr:
                case SpecialType.System_UIntPtr:
                case SpecialType.System_DateTime:
                    return true;
                default:
                    return false;
            }
        }

        private static bool InheritsFromOrImplements(ITypeSymbol type, ITypeSymbol baseType)
        {
            // Check inheritance chain
            for (var current = type; current != null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, baseType))
                    return true;
            }

            // Check interfaces
            foreach (var interfaceType in type.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(interfaceType, baseType))
                    return true;
            }

            return false;
        }
    }
}