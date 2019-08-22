// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using APIView;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ApiView
{
    public class CodeFileBuilder
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
        public CodeFile Build(IAssemblySymbol assemblySymbol)
        {
            var assemblyItems = new List<NavigationItem>();
            var builder = new CodeFileTokensBuilder();
            foreach (var namespaceSymbol in EnumerateNamespaces(assemblySymbol))
            {
                if (namespaceSymbol.IsGlobalNamespace)
                {
                    foreach (var namedTypeSymbol in SortTypes(namespaceSymbol.GetTypeMembers()))
                    {
                        BuildType(builder, namedTypeSymbol, assemblyItems);
                    }
                }
                else
                {
                    Build(builder, namespaceSymbol, assemblyItems);
                }
            }

            var node = new CodeFile()
            {
                Tokens = builder.Tokens.ToArray(),
                Version = CodeFile.CurrentVersion,
                Navigation = assemblyItems,
            };
            return node;
        }

        private void Build(CodeFileTokensBuilder builder, INamespaceSymbol namespaceSymbol, List<NavigationItem> navigationBuilder)
        {
            builder.Keyword(SyntaxKind.NamespaceKeyword);
            builder.Space();
            BuildNamespaceName(builder, namespaceSymbol);

            builder.Space();
            builder.Punctuation(SyntaxKind.OpenBraceToken);
            builder.IncrementIndent();
            builder.NewLine();

            List<NavigationItem> namespaceItems = new List<NavigationItem>();
            foreach (var namedTypeSymbol in namespaceSymbol.GetTypeMembers())
            {
                BuildType(builder, namedTypeSymbol, namespaceItems);
            }

            CloseBrace(builder);
            
            var namespaceItem = new NavigationItem()
            {
                NavigationId = GetId(namespaceSymbol),
                Text = namespaceSymbol.ToDisplayString(),
                ChildItems = namespaceItems.ToArray()
            };
            navigationBuilder.Add(namespaceItem);
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

        private void BuildType(CodeFileTokensBuilder builder, INamedTypeSymbol namedType, List<NavigationItem> navigationBuilder)
        {
            if (!IsAccessible(namedType))
            {
                return;
            }

            navigationBuilder.Add(new NavigationItem()
            {
                NavigationId = GetId(namedType),
                Text = namedType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
            });

            builder.WriteIndent();
            NodeFromSymbol(builder, namedType, true);
            if (namedType.TypeKind == TypeKind.Delegate)
            {
                builder.Punctuation(SyntaxKind.SemicolonToken);
                builder.NewLine();
                return;
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
                return -1;
            }

            if (typeSymbol.Name.EndsWith("Extensions"))
            {
                return 1;
            }

            if (typeSymbol.Name.EndsWith("Exception"))
            {
                return 2;
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
                    return Convert.ToInt32(fieldSymbol.ConstantValue);
                
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
                DefinitionId = GetId(symbol),
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

        private static string GetId(ISymbol namedType)
        {
            return namedType.ToDisplayString(_idFormat);
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
                navigateToId = GetId(symbol);
            }

            return new CodeFileToken()
            {
                DefinitionId =  containingSymbol.Equals(symbol) ? GetId(containingSymbol) : null,
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