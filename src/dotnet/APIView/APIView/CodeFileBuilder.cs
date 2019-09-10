// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using APIView;
using APIView.Analysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ApiView
{
    public static class SymbolExtensions
    {
        static SymbolDisplayFormat _idFormat = new SymbolDisplayFormat(
            SymbolDisplayGlobalNamespaceStyle.Omitted,
            SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            SymbolDisplayGenericsOptions.IncludeTypeParameters,
            SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeParameters,
            SymbolDisplayDelegateStyle.NameAndParameters,
            SymbolDisplayExtensionMethodStyle.StaticMethod,
            SymbolDisplayParameterOptions.IncludeType,
            SymbolDisplayPropertyStyle.NameOnly,
            SymbolDisplayLocalOptions.None,
            SymbolDisplayKindOptions.None,
            SymbolDisplayMiscellaneousOptions.None);

        public static string GetId(this ISymbol namedType)
        {
            return namedType.ToDisplayString(_idFormat);
        }
    }

    public class CodeFileBuilder
    {
        SymbolDisplayFormat _defaultDisplayFormat = new SymbolDisplayFormat(
            SymbolDisplayGlobalNamespaceStyle.Omitted,
            delegateStyle: SymbolDisplayDelegateStyle.NameAndSignature,
            extensionMethodStyle: SymbolDisplayExtensionMethodStyle.StaticMethod,
            propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.AllowDefaultLiteral |
                                  SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                                  SymbolDisplayMiscellaneousOptions.RemoveAttributeSuffix |
                                  SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                                  SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier,
            kindOptions: SymbolDisplayKindOptions.IncludeMemberKeyword |
                         SymbolDisplayKindOptions.IncludeTypeKeyword,
            parameterOptions: SymbolDisplayParameterOptions.IncludeDefaultValue |
                              SymbolDisplayParameterOptions.IncludeExtensionThis |
                              SymbolDisplayParameterOptions.IncludeName |
                              SymbolDisplayParameterOptions.IncludeParamsRefOut |
                              SymbolDisplayParameterOptions.IncludeType,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeConstraints |
                             SymbolDisplayGenericsOptions.IncludeTypeConstraints |
                             SymbolDisplayGenericsOptions.IncludeTypeParameters |
                             SymbolDisplayGenericsOptions.IncludeVariance,
            memberOptions: SymbolDisplayMemberOptions.IncludeExplicitInterface |
                           SymbolDisplayMemberOptions.IncludeAccessibility |
                           SymbolDisplayMemberOptions.IncludeConstantValue |
                           SymbolDisplayMemberOptions.IncludeModifiers |
                           SymbolDisplayMemberOptions.IncludeParameters |
                           SymbolDisplayMemberOptions.IncludeRef |
                           SymbolDisplayMemberOptions.IncludeType,
            localOptions: SymbolDisplayLocalOptions.IncludeConstantValue |
                          SymbolDisplayLocalOptions.IncludeRef |
                          SymbolDisplayLocalOptions.IncludeType
        );

        private IEnumerable<INamespaceSymbol> EnumerateNamespaces(IAssemblySymbol assemblySymbol)
        {
            var stack = new Stack<INamespaceSymbol>();
            stack.Push(assemblySymbol.GlobalNamespace);
            while (stack.TryPop(out var currentNamespace))
            {
                if (HasAnyPublicTypes(currentNamespace))
                {
                    yield return currentNamespace;
                }
                foreach (var subNamespace in currentNamespace.GetNamespaceMembers())
                {
                    stack.Push(subNamespace);
                }
            }
        }
        public (CodeFile, AnalysisResult[]) Build(IAssemblySymbol assemblySymbol, bool runAnalysis)
        {
            var analyzer = new Analyzer(assemblySymbol);

            var builder = new CodeFileTokensBuilder();
            var navigationItems = new List<NavigationItem>();
            foreach (var namespaceSymbol in EnumerateNamespaces(assemblySymbol))
            {
                if (namespaceSymbol.IsGlobalNamespace)
                {
                    foreach (var namedTypeSymbol in SortTypes(namespaceSymbol.GetTypeMembers()))
                    {
                        var typeId = BuildType(builder, namedTypeSymbol, navigationItems);
                        if (runAnalysis)
                        {
                            analyzer.Analyze(namedTypeSymbol);
                        }
                    }
                }
                else
                {
                    var namespaceId = Build(builder, namespaceSymbol, navigationItems, analyzer);
                }
            }

            NavigationItem assemblyNavigationItem = new NavigationItem()
            {
                Text = assemblySymbol.Name + ".dll",
                ChildItems = navigationItems.ToArray(),
                Tags = { {"TypeKind", "assembly"} }
            };

            var node = new CodeFile()
            {
                Name = assemblySymbol.Name,
                Tokens = builder.Tokens.ToArray(),
                Version = CodeFile.CurrentVersion,
                Navigation = new List<NavigationItem>() { assemblyNavigationItem },
            };

            return (node, analyzer.CreateResults());
        }

        private string Build(CodeFileTokensBuilder builder, INamespaceSymbol namespaceSymbol, List<NavigationItem> navigationItems, Analyzer analyser)
        {
            builder.Keyword(SyntaxKind.NamespaceKeyword);
            builder.Space();
            BuildNamespaceName(builder, namespaceSymbol);

            builder.Space();
            builder.Punctuation(SyntaxKind.OpenBraceToken);
            builder.IncrementIndent();
            builder.NewLine();

            List<NavigationItem> namespaceItems = new List<NavigationItem>();
            foreach (var namedTypeSymbol in SortTypes(namespaceSymbol.GetTypeMembers()))
            {
                var typeId = BuildType(builder, namedTypeSymbol, namespaceItems);
                analyser.Analyze(namedTypeSymbol);
            }

            CloseBrace(builder);

            var namespaceItem = new NavigationItem()
            {
                NavigationId = namespaceSymbol.GetId(),
                Text = namespaceSymbol.ToDisplayString(),
                ChildItems = namespaceItems.ToArray()
            };
            navigationItems.Add(namespaceItem);
            return namespaceItem.NavigationId;
        }

        private void BuildNamespaceName(CodeFileTokensBuilder builder, INamespaceSymbol namespaceSymbol)
        {
            if (!namespaceSymbol.ContainingNamespace.IsGlobalNamespace)
            {
                BuildNamespaceName(builder, namespaceSymbol.ContainingNamespace);
                builder.Punctuation(SyntaxKind.DotToken);
            }
            NodeFromSymbol(builder, namespaceSymbol);
        }

        private bool HasAnyPublicTypes(INamespaceSymbol subNamespaceSymbol)
        {
            return subNamespaceSymbol.GetTypeMembers().Any(t => IsAccessible(t));
        }

        private string BuildType(CodeFileTokensBuilder builder, INamedTypeSymbol namedType, List<NavigationItem> navigationBuilder)
        {
            if (!IsAccessible(namedType))
            {
                return null;
            }

            var navigationItem = new NavigationItem()
            {
                NavigationId = namedType.GetId(),
                Text = namedType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            };
            navigationBuilder.Add(navigationItem);
            navigationItem.Tags.Add("TypeKind", namedType.TypeKind.ToString().ToLowerInvariant());

            builder.WriteIndent();
            NodeFromSymbol(builder, namedType, true);
            if (namedType.TypeKind == TypeKind.Delegate)
            {
                builder.Punctuation(SyntaxKind.SemicolonToken);
                builder.NewLine();
                return navigationItem.NavigationId;
            }

            builder.Space();
            builder.Punctuation(SyntaxKind.OpenBraceToken);
            builder.IncrementIndent();
            builder.NewLine();

            foreach (var namedTypeSymbol in SortTypes(namedType.GetTypeMembers()))
            {
                BuildType(builder, namedTypeSymbol, navigationBuilder);
            }

            foreach (var member in SortMembers(namedType.GetMembers()))
            {
                if (member.Kind == SymbolKind.NamedType || member.IsImplicitlyDeclared || !IsAccessible(member)) continue;
                if (member is IMethodSymbol method)
                {
                    if (method.MethodKind == MethodKind.PropertyGet ||
                        method.MethodKind == MethodKind.PropertySet ||
                        method.MethodKind == MethodKind.EventAdd ||
                        method.MethodKind == MethodKind.EventRemove ||
                        method.MethodKind == MethodKind.EventRaise)
                    {
                        continue;
                    }
                }
                BuildMember(builder, member);
            }

            CloseBrace(builder);
            return navigationItem.NavigationId;
        }

        private static void CloseBrace(CodeFileTokensBuilder builder)
        {
            builder.DecrementIndent();
            builder.WriteIndent();
            builder.Punctuation(SyntaxKind.CloseBraceToken);
            builder.NewLine();
        }

        private void BuildMember(CodeFileTokensBuilder builder, ISymbol member)
        {
            builder.WriteIndent();
            NodeFromSymbol(builder, member);
            if (member.Kind == SymbolKind.Method)
            {
                builder.Space();
                builder.Punctuation(SyntaxKind.OpenBraceToken);
                builder.Punctuation(SyntaxKind.CloseBraceToken);
            }
            else if (member.Kind == SymbolKind.Field && member.ContainingType.TypeKind == TypeKind.Enum)
            {
                builder.Punctuation(SyntaxKind.CommaToken);
            }
            else if (member.Kind != SymbolKind.Property)
            {
                builder.Punctuation(SyntaxKind.SemicolonToken);
            }

            builder.NewLine();
        }

        private IEnumerable<T> SortTypes<T>(IEnumerable<T> symbols) where T: ITypeSymbol
        {
            return symbols.OrderBy(t => (GetTypeOrder(t), t.DeclaredAccessibility != Accessibility.Public, t.Name));
        }

        private IEnumerable<ISymbol> SortMembers(IEnumerable<ISymbol> members)
        {
            return members.OrderBy(t => (GetMemberOrder(t), t.DeclaredAccessibility != Accessibility.Public, t.Name));
        }

        private static int GetTypeOrder(ITypeSymbol typeSymbol)
        {
            if (typeSymbol.Name.EndsWith("Client"))
            {
                return -100;
            }

            if (typeSymbol.Name.EndsWith("Options")) {
                return -20;
            }

            if (typeSymbol.Name.EndsWith("Extensions"))
            {
                return 1;
            }

            if (typeSymbol.TypeKind == TypeKind.Interface) {
                return -1;
            }
            if (typeSymbol.TypeKind == TypeKind.Enum) {
                return 90;
            }
            if (typeSymbol.TypeKind == TypeKind.Delegate) {
                return 99;
            }
            if (typeSymbol.Name.EndsWith("Exception"))
            {
                return 100;
            }

            // Nested type
            if (typeSymbol.ContainingType != null)
            {
                return 3;
            }

            return 0;
        }

        private static int GetMemberOrder(ISymbol symbol)
        {
            switch (symbol)
            {
                case IFieldSymbol fieldSymbol when fieldSymbol.ContainingType.TypeKind == TypeKind.Enum:
                    return (int)Convert.ToInt64(fieldSymbol.ConstantValue);

                case IMethodSymbol methodSymbol when methodSymbol.MethodKind == MethodKind.Constructor:
                    return -10;

                case IMethodSymbol methodSymbol when (methodSymbol.OverriddenMethod?.ContainingType?.SpecialType == SpecialType.System_Object ||
                                                      methodSymbol.OverriddenMethod?.ContainingType?.SpecialType == SpecialType.System_ValueType):
                    return 5;
                case IMethodSymbol methodSymbol when methodSymbol.IsStatic:
                    return -4;
                case IPropertySymbol _:
                    return -5;
                case IFieldSymbol _:
                    return -6;
                default:
                    return 0;
            }
        }

        private void NodeFromSymbol(CodeFileTokensBuilder builder, ISymbol symbol,  bool prependVisibility = false)
        {
            builder.Append(new CodeFileToken()
            {
                DefinitionId = symbol.GetId(),
                Kind = CodeFileTokenKind.LineIdMarker
            });
            if (prependVisibility)
            {
                builder.Keyword(SyntaxFacts.GetText(ToEffectiveAccessibility(symbol.DeclaredAccessibility)));
                builder.Space();
            }
            foreach (var symbolDisplayPart in symbol.ToDisplayParts(_defaultDisplayFormat))
            {
                builder.Append(MapToken(symbol, symbolDisplayPart));
            }
        }

        private CodeFileToken MapToken(ISymbol containingSymbol, SymbolDisplayPart symbolDisplayPart)
        {
            CodeFileTokenKind kind;

            switch (symbolDisplayPart.Kind)
            {
                case SymbolDisplayPartKind.TypeParameterName:
                case SymbolDisplayPartKind.AliasName:
                case SymbolDisplayPartKind.AssemblyName:
                case SymbolDisplayPartKind.ClassName:
                case SymbolDisplayPartKind.DelegateName:
                case SymbolDisplayPartKind.EnumName:
                case SymbolDisplayPartKind.ErrorTypeName:
                case SymbolDisplayPartKind.InterfaceName:
                case SymbolDisplayPartKind.StructName:
                    kind = CodeFileTokenKind.TypeName;
                    break;
                case SymbolDisplayPartKind.Keyword:
                    kind = CodeFileTokenKind.Keyword;
                    break;
                case SymbolDisplayPartKind.LineBreak:
                    kind = CodeFileTokenKind.Newline;
                    break;
                case SymbolDisplayPartKind.StringLiteral:
                    kind = CodeFileTokenKind.StringLiteral;
                    break;
                case SymbolDisplayPartKind.Punctuation:
                    kind = CodeFileTokenKind.Punctuation;
                    break;
                case SymbolDisplayPartKind.Space:
                    kind = CodeFileTokenKind.Whitespace;
                    break;
                case SymbolDisplayPartKind.PropertyName:
                case SymbolDisplayPartKind.EventName:
                case SymbolDisplayPartKind.FieldName:
                case SymbolDisplayPartKind.MethodName:
                case SymbolDisplayPartKind.Operator:
                case SymbolDisplayPartKind.EnumMemberName:
                case SymbolDisplayPartKind.ExtensionMethodName:
                case SymbolDisplayPartKind.ConstantName:
                    kind = CodeFileTokenKind.MemberName;
                    break;
                default:
                    kind = CodeFileTokenKind.Text;
                    break;
            }

            string navigateToId = null;
            var symbol = symbolDisplayPart.Symbol;

            if (symbol is INamedTypeSymbol &&
                !containingSymbol.Equals(symbol) &&
                containingSymbol.ContainingAssembly.Equals(symbol.ContainingAssembly))
            {
                navigateToId = symbol.GetId();
            }

            return new CodeFileToken()
            {
                DefinitionId =  containingSymbol.Equals(symbol) ? containingSymbol.GetId() : null,
                NavigateToId = navigateToId,
                Value = symbolDisplayPart.ToString(),
                Kind =  kind
            };
        }

        private Accessibility ToEffectiveAccessibility(Accessibility accessibility)
        {
            switch (accessibility)
            {
                case Accessibility.ProtectedAndInternal:
                case Accessibility.ProtectedOrInternal:
                    return Accessibility.Protected;
                default:
                    return accessibility;
            }
        }

        private bool IsAccessible(ISymbol s)
        {
            switch (s.DeclaredAccessibility)
            {
                case Accessibility.ProtectedAndInternal:
                case Accessibility.Protected:
                case Accessibility.ProtectedOrInternal:
                case Accessibility.Public:
                    return true;
                default:
                    return false;
            }
        }
    }
}