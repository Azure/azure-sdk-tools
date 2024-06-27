// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIView;
using APIView.Analysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.SymbolDisplay;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;

namespace ApiView
{
    public class CodeFileBuilder
    {
        private static readonly char[] _newlineChars = new char[] { '\r', '\n' };

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
            kindOptions: SymbolDisplayKindOptions.IncludeMemberKeyword,
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
                           SymbolDisplayMemberOptions.IncludeConstantValue |
                           SymbolDisplayMemberOptions.IncludeModifiers |
                           SymbolDisplayMemberOptions.IncludeParameters |
                           SymbolDisplayMemberOptions.IncludeType
        );

        private IAssemblySymbol _assembly;

        public ICodeFileBuilderSymbolOrderProvider SymbolOrderProvider { get; set; } = new CodeFileBuilderSymbolOrderProvider();

        public const string CurrentVersion = "26";

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

        public CodeFile Build(IAssemblySymbol assemblySymbol, bool runAnalysis, List<DependencyInfo> dependencies)
        {
            _assembly = assemblySymbol;
            var analyzer = new Analyzer();

            if (runAnalysis)
            {
                analyzer.VisitAssembly(assemblySymbol);
            }
            var builder = new CodeFileTokensBuilder();

            BuildDependencies(builder, dependencies);
            BuildInternalsVisibleToAttributes(builder, assemblySymbol);

            var navigationItems = new List<NavigationItem>();
            foreach (var namespaceSymbol in SymbolOrderProvider.OrderNamespaces(EnumerateNamespaces(assemblySymbol)))
            {
                if (namespaceSymbol.IsGlobalNamespace)
                {
                    foreach (var namedTypeSymbol in SymbolOrderProvider.OrderTypes(namespaceSymbol.GetTypeMembers()))
                    {
                        BuildType(builder, namedTypeSymbol, navigationItems, false);
                    }
                }
                else
                {
                    BuildNamespace(builder, namespaceSymbol, navigationItems);
                }
            }

            NavigationItem assemblyNavigationItem = new NavigationItem()
            {
                Text = assemblySymbol.Name + ".dll",
                ChildItems = navigationItems.ToArray(),
                Tags = { { "TypeKind", "assembly" } }
            };

            var node = new CodeFile()
            {
                Name = $"{assemblySymbol.Name} ({assemblySymbol.Identity.Version})",
                Language = "C#",
                Tokens = builder.Tokens.ToArray(),
                VersionString = CurrentVersion,
                Navigation = new[] { assemblyNavigationItem },
                Diagnostics = analyzer.Results.ToArray(),
                PackageName = assemblySymbol.Name,
                PackageVersion = assemblySymbol.Identity.Version.ToString()
            };

            return node;
        }

        public static void BuildInternalsVisibleToAttributes(CodeFileTokensBuilder builder, IAssemblySymbol assemblySymbol)
        {
            var assemblyAttributes = assemblySymbol.GetAttributes()
                .Where(a =>
                    a.AttributeClass.Name == "InternalsVisibleToAttribute" &&
                    !a.ConstructorArguments[0].Value.ToString().Contains(".Tests") &&
                    !a.ConstructorArguments[0].Value.ToString().Contains(".Perf") &&
                    !a.ConstructorArguments[0].Value.ToString().Contains("DynamicProxyGenAssembly2"));
            if (assemblyAttributes != null && assemblyAttributes.Any())
            {
                builder.Append("Exposes internals to:", CodeFileTokenKind.Text);
                builder.NewLine();
                foreach (AttributeData attribute in assemblyAttributes)
                {
                    if (attribute.ConstructorArguments.Length > 0)
                    {
                        var param = attribute.ConstructorArguments[0].Value.ToString();
                        var firstComma = param.IndexOf(',');
                        param = firstComma > 0 ? param[..firstComma] : param;
                        builder.Append(new CodeFileToken(param, CodeFileTokenKind.Text)
                        {
                            // allow assembly to have a comment
                            DefinitionId = attribute.AttributeClass.Name
                        });
                    }
                    builder.NewLine();
                }
                builder.NewLine();
            }
        }

        public static void BuildDependencies(CodeFileTokensBuilder builder, List<DependencyInfo> dependencies)
        {
            if (dependencies != null && dependencies.Any())
            {
                builder.NewLine();
                builder.Append("Dependencies:", CodeFileTokenKind.Text);
                builder.NewLine();
                foreach (DependencyInfo dependency in dependencies)
                {
                    builder.Append(new CodeFileToken(dependency.Name, CodeFileTokenKind.Text)
                    {
                        // allow dependency to be commentable
                        DefinitionId = dependency.Name
                    });
                    // don't include the version in the API sign-off diffs
                    builder.Append(null, CodeFileTokenKind.SkipDiffRangeStart);
                    builder.Append($"-{dependency.Version}", CodeFileTokenKind.Text);
                    builder.Append(null, CodeFileTokenKind.SkipDiffRangeEnd);
                    builder.NewLine();
                }

                builder.NewLine();
            }
        }

        private void BuildNamespace(CodeFileTokensBuilder builder, INamespaceSymbol namespaceSymbol, List<NavigationItem> navigationItems)
        {
            bool isHidden = HasOnlyHiddenTypes(namespaceSymbol);

            if (isHidden)
            {
                builder.Append(null, CodeFileTokenKind.HiddenApiRangeStart);
            }
            builder.Keyword(SyntaxKind.NamespaceKeyword);
            builder.Space();
            BuildNamespaceName(builder, namespaceSymbol);

            builder.Space();
            builder.Punctuation(SyntaxKind.OpenBraceToken);
            builder.IncrementIndent();
            builder.NewLine();

            List<NavigationItem> namespaceItems = new List<NavigationItem>();
            foreach (var namedTypeSymbol in SymbolOrderProvider.OrderTypes(namespaceSymbol.GetTypeMembers()))
            {
                BuildType(builder, namedTypeSymbol, namespaceItems, isHidden);
            }

            CloseBrace(builder);

            var namespaceItem = new NavigationItem()
            {
                NavigationId = namespaceSymbol.GetId(),
                Text = namespaceSymbol.ToDisplayString(),
                ChildItems = namespaceItems.ToArray(),
                Tags = { { "TypeKind", "namespace" } },
                IsHiddenApi = isHidden
            };
            navigationItems.Add(namespaceItem);
            if (isHidden)
            {
                builder.Append(null, CodeFileTokenKind.HiddenApiRangeEnd);
            }
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
            return subNamespaceSymbol.GetTypeMembers().Any(IsAccessible);
        }

        private bool HasOnlyHiddenTypes(INamespaceSymbol namespaceSymbol)
        {
            return namespaceSymbol.GetTypeMembers().All(t=> IsHiddenFromIntellisense(t) || !IsAccessible(t));
        }

        private void BuildType(CodeFileTokensBuilder builder, INamedTypeSymbol namedType, List<NavigationItem> navigationBuilder, bool inHiddenScope)
        {
            if (!IsAccessible(namedType))
            {
                return;
            }

            bool isHidden = IsHiddenFromIntellisense(namedType);
            var navigationItem = new NavigationItem()
            {
                NavigationId = namedType.GetId(),
                Text = namedType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                IsHiddenApi = isHidden
            };
            navigationBuilder.Add(navigationItem);
            navigationItem.Tags.Add("TypeKind", namedType.TypeKind.ToString().ToLowerInvariant());

            if (isHidden && !inHiddenScope)
            {
                builder.Append(null, CodeFileTokenKind.HiddenApiRangeStart);
            }

            BuildDocumentation(builder, namedType);
            BuildAttributes(builder, namedType.GetAttributes());

            builder.WriteIndent();
            BuildVisibility(builder, namedType);

            builder.Space();

            switch (namedType.TypeKind)
            {
                case TypeKind.Class:
                    BuildClassModifiers(builder, namedType);
                    builder.Keyword(SyntaxKind.ClassKeyword);
                    break;
                case TypeKind.Delegate:
                    builder.Keyword(SyntaxKind.DelegateKeyword);
                    break;
                case TypeKind.Enum:
                    builder.Keyword(SyntaxKind.EnumKeyword);
                    break;
                case TypeKind.Interface:
                    builder.Keyword(SyntaxKind.InterfaceKeyword);
                    break;
                case TypeKind.Struct:
                    if (namedType.IsReadOnly)
                    {
                        builder.Keyword(SyntaxKind.ReadOnlyKeyword);
                        builder.Space();
                    }
                    builder.Keyword(SyntaxKind.StructKeyword);
                    break;
            }

            builder.Space();

            NodeFromSymbol(builder, namedType);
            if (namedType.TypeKind == TypeKind.Delegate)
            {
                builder.Punctuation(SyntaxKind.SemicolonToken);
                builder.NewLine();
                return;
            }

            builder.Space();

            BuildBaseType(builder, namedType);

            builder.Punctuation(SyntaxKind.OpenBraceToken);
            builder.IncrementIndent();
            builder.NewLine();

            foreach (var namedTypeSymbol in SymbolOrderProvider.OrderTypes(namedType.GetTypeMembers()))
            {
                BuildType(builder, namedTypeSymbol, navigationBuilder, inHiddenScope || isHidden);
            }

            foreach (var member in SymbolOrderProvider.OrderMembers(namedType.GetMembers()))
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

                BuildMember(builder, member, inHiddenScope);
            }

            CloseBrace(builder);

            if (isHidden && !inHiddenScope)
            {
                builder.Append(null, CodeFileTokenKind.HiddenApiRangeEnd);
            }
        }

        private void BuildDocumentation(CodeFileTokensBuilder builder, ISymbol symbol)
        {
            var lines = symbol.GetDocumentationCommentXml().Trim().Split(_newlineChars);
            if (lines.All(string.IsNullOrWhiteSpace))
            {
                return;
            }
            builder.Append(null, CodeFileTokenKind.DocumentRangeStart);
            foreach (var line in lines)
            {
                builder.WriteIndent();
                builder.Comment("// " + line.Trim());
                builder.NewLine();
            }
            builder.Append(null, CodeFileTokenKind.DocumentRangeEnd);
        }

        private static void BuildClassModifiers(CodeFileTokensBuilder builder, INamedTypeSymbol namedType)
        {
            if (namedType.IsAbstract)
            {
                builder.Keyword(SyntaxKind.AbstractKeyword);
                builder.Space();
            }

            if (namedType.IsStatic)
            {
                builder.Keyword(SyntaxKind.StaticKeyword);
                builder.Space();
            }

            if (namedType.IsSealed)
            {
                builder.Keyword(SyntaxKind.SealedKeyword);
                builder.Space();
            }
        }

        private void BuildBaseType(CodeFileTokensBuilder builder, INamedTypeSymbol namedType)
        {
            bool first = true;

            if (namedType.BaseType != null &&
                namedType.BaseType.SpecialType == SpecialType.None)
            {
                builder.Punctuation(SyntaxKind.ColonToken);
                builder.Space();
                first = false;

                DisplayName(builder, namedType.BaseType);
            }

            foreach (var typeInterface in namedType.Interfaces)
            {
                if (!IsAccessible(typeInterface)) continue;

                if (!first)
                {
                    builder.Punctuation(SyntaxKind.CommaToken);
                    builder.Space();
                }
                else
                {
                    builder.Punctuation(SyntaxKind.ColonToken);
                    builder.Space();
                    first = false;
                }

                DisplayName(builder, typeInterface);
            }

            if (!first)
            {
                builder.Space();
            }
        }

        private static void CloseBrace(CodeFileTokensBuilder builder)
        {
            builder.DecrementIndent();
            builder.WriteIndent();
            builder.Punctuation(SyntaxKind.CloseBraceToken);
            builder.NewLine();
        }

        private void BuildMember(CodeFileTokensBuilder builder, ISymbol member, bool inHiddenScope)
        {
            bool isHidden = IsHiddenFromIntellisense(member);

            if (isHidden && !inHiddenScope)
            {
                builder.Append(null, CodeFileTokenKind.HiddenApiRangeStart);
            }

            BuildDocumentation(builder, member);
            BuildAttributes(builder, member.GetAttributes());

            builder.WriteIndent();
            NodeFromSymbol(builder, member);

            if (member.Kind == SymbolKind.Field && member.ContainingType.TypeKind == TypeKind.Enum)
            {
                builder.Punctuation(SyntaxKind.CommaToken);
            }
            else if (member.Kind != SymbolKind.Property)
            {
                builder.Punctuation(SyntaxKind.SemicolonToken);
            }

            builder.NewLine();
            if (isHidden && !inHiddenScope)
            {
                builder.Append(null, CodeFileTokenKind.HiddenApiRangeEnd);
            }
        }

        private void BuildAttributes(CodeFileTokensBuilder builder, ImmutableArray<AttributeData> attributes)
        {
            const string attributeSuffix = "Attribute";
            foreach (var attribute in attributes)
            {
                if ((!IsAccessible(attribute.AttributeClass) &&
                    attribute.AttributeClass.Name != "FriendAttribute" && 
                    attribute.AttributeClass.ContainingNamespace.ToString() != "System.Diagnostics.CodeAnalysis")
                    || IsSkippedAttribute(attribute.AttributeClass))
                {
                    continue;
                }
                builder.WriteIndent();
                if(attribute.AttributeClass.DeclaredAccessibility == Accessibility.Internal || attribute.AttributeClass.DeclaredAccessibility == Accessibility.Friend)
                {
                    builder.Keyword("internal");
                    builder.Space();
                }
                builder.Punctuation(SyntaxKind.OpenBracketToken);
                var name = attribute.AttributeClass.Name;
                if (name.EndsWith(attributeSuffix))
                {
                    name = name.Substring(0, name.Length - attributeSuffix.Length);
                }
                builder.Append(name, CodeFileTokenKind.TypeName);
                if (attribute.ConstructorArguments.Any())
                {
                    builder.Punctuation(SyntaxKind.OpenParenToken);
                    bool first = true;

                    foreach (var argument in attribute.ConstructorArguments)
                    {
                        if (!first)
                        {
                            builder.Punctuation(SyntaxKind.CommaToken);
                            builder.Space();
                        }
                        else
                        {
                            first = false;
                        }
                        BuildTypedConstant(builder, argument);
                    }

                    foreach (var argument in attribute.NamedArguments)
                    {
                        if (!first)
                        {
                            builder.Punctuation(SyntaxKind.CommaToken);
                            builder.Space();
                        }
                        else
                        {
                            first = false;
                        }
                        builder.Append(argument.Key, CodeFileTokenKind.Text);
                        builder.Space();
                        builder.Punctuation(SyntaxKind.EqualsToken);
                        builder.Space();
                        BuildTypedConstant(builder, argument.Value);
                    }

                    builder.Punctuation(SyntaxKind.CloseParenToken);
                }
                builder.Punctuation(SyntaxKind.CloseBracketToken);
                builder.NewLine();
            }
        }

        private bool IsSkippedAttribute(INamedTypeSymbol attributeAttributeClass)
        {
            switch (attributeAttributeClass.Name)
            {
                case "DebuggerStepThroughAttribute":
                case "AsyncStateMachineAttribute":
                case "IteratorStateMachineAttribute":
                case "DefaultMemberAttribute":
                case "AsyncIteratorStateMachineAttribute":
                case "EditorBrowsableAttribute":
                case "NullableAttribute":
                case "NullableContextAttribute":
                    return true;
                default:
                    return false;
            }
        }

        private bool IsHiddenFromIntellisense(ISymbol member) =>
            member.GetAttributes().Any(d => d.AttributeClass?.Name == "EditorBrowsableAttribute"
                                            && (EditorBrowsableState) d.ConstructorArguments[0].Value == EditorBrowsableState.Never);

        private bool IsDecoratedWithAttribute(ISymbol member, string attributeName) =>
            member.GetAttributes().Any(d => d.AttributeClass?.Name == attributeName);

        private void BuildTypedConstant(CodeFileTokensBuilder builder, TypedConstant typedConstant)
        {
            if (typedConstant.IsNull)
            {
                builder.Keyword(SyntaxKind.NullKeyword);
            }
            else if (typedConstant.Kind == TypedConstantKind.Enum)
            {
                new CodeFileBuilderEnumFormatter(builder).Format(typedConstant.Type, typedConstant.Value);
            }
            else if (typedConstant.Kind == TypedConstantKind.Type)
            {
                builder.Keyword(SyntaxKind.TypeOfKeyword);
                builder.Punctuation("(");
                DisplayName(builder, (ITypeSymbol)typedConstant.Value);
                builder.Punctuation(")");
            }
            else if (typedConstant.Kind == TypedConstantKind.Array)
            {
                builder.Keyword(SyntaxKind.NewKeyword);
                builder.Punctuation("[] {");

                bool first = true;

                foreach (var value in typedConstant.Values)
                {
                    if (!first)
                    {
                        builder.Punctuation(SyntaxKind.CommaToken);
                        builder.Space();
                    }
                    else
                    {
                        first = false;
                    }

                    BuildTypedConstant(builder, value);
                }
                builder.Punctuation("}");
            }
            else
            {
                if (typedConstant.Value is string s)
                {
                    builder.Append(
                        ObjectDisplay.FormatLiteral(s, ObjectDisplayOptions.UseQuotes | ObjectDisplayOptions.EscapeNonPrintableCharacters),
                        CodeFileTokenKind.StringLiteral);
                }
                else
                {
                    builder.Append(
                        ObjectDisplay.FormatPrimitive(typedConstant.Value, ObjectDisplayOptions.None),
                        CodeFileTokenKind.Literal);
                }
            }
        }

        private void NodeFromSymbol(CodeFileTokensBuilder builder, ISymbol symbol)
        {
            builder.Append(new CodeFileToken()
            {
                DefinitionId = symbol.GetId(),
                Kind = CodeFileTokenKind.LineIdMarker
            });
            DisplayName(builder, symbol, symbol);
        }

        private void BuildVisibility(CodeFileTokensBuilder builder, ISymbol symbol)
        {
            builder.Keyword(SyntaxFacts.GetText(ToEffectiveAccessibility(symbol.DeclaredAccessibility)));
        }

        private void DisplayName(CodeFileTokensBuilder builder, ISymbol symbol, ISymbol definedSymbol = null)
        {
            if (NeedsAccessibility(symbol))
            {
                builder.Keyword(SyntaxFacts.GetText(ToEffectiveAccessibility(symbol.DeclaredAccessibility)));
                builder.Space();
            }
            if (symbol is IPropertySymbol propSymbol && propSymbol.DeclaredAccessibility != Accessibility.Internal)
            {
                var parts = propSymbol.ToDisplayParts(_defaultDisplayFormat);
                for (int i = 0; i < parts.Length; i++)
                {
                    // Skip internal setters
                    if (parts[i].Kind == SymbolDisplayPartKind.Keyword && parts[i].ToString() == "internal")
                    {
                        while (i < parts.Length && parts[i].ToString() != "}")
                        {
                            i++;
                        }
                    }
                    builder.Append(MapToken(definedSymbol, parts[i]));
                }
            }
            else
            {
                foreach (var symbolDisplayPart in symbol.ToDisplayParts(_defaultDisplayFormat))
                {
                    builder.Append(MapToken(definedSymbol, symbolDisplayPart));
                }
            }
        }

        private bool NeedsAccessibility(ISymbol symbol)
        {
            return symbol switch
            {
                INamespaceSymbol => false,
                INamedTypeSymbol => false,
                IFieldSymbol fieldSymbol => fieldSymbol.ContainingType.TypeKind != TypeKind.Enum,
                IMethodSymbol methodSymbol => !methodSymbol.ExplicitInterfaceImplementations.Any() &&
                                              methodSymbol.ContainingType.TypeKind != TypeKind.Interface,
                IPropertySymbol propertySymbol => !propertySymbol.ExplicitInterfaceImplementations.Any() &&
                                                  propertySymbol.ContainingType.TypeKind != TypeKind.Interface,
                _ => true
            };
        }

        private CodeFileToken MapToken(ISymbol definedSymbol, SymbolDisplayPart symbolDisplayPart)
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
                (definedSymbol == null || !SymbolEqualityComparer.Default.Equals(definedSymbol, symbol)) &&
                SymbolEqualityComparer.Default.Equals(_assembly, symbol.ContainingAssembly))
            {
                navigateToId = symbol.GetId();
            }

            return new CodeFileToken()
            {
                DefinitionId = (definedSymbol != null && SymbolEqualityComparer.Default.Equals(definedSymbol, symbol)) ? definedSymbol.GetId() : null,
                NavigateToId = navigateToId,
                Value = symbolDisplayPart.ToString(),
                Kind = kind
            };
        }

        private Accessibility ToEffectiveAccessibility(Accessibility accessibility)
        {
            switch (accessibility)
            {
                case Accessibility.ProtectedAndInternal:
                    return Accessibility.Internal;
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
                case Accessibility.Protected:
                case Accessibility.ProtectedOrInternal:
                case Accessibility.Public:
                    return true;
                case Accessibility.Internal:
                    return s.GetAttributes().Any(a => a.AttributeClass.Name == "FriendAttribute");
                default:
                    return IsAccessibleExplicitInterfaceImplementation(s);
            }
        }

        private bool IsAccessibleExplicitInterfaceImplementation(ISymbol s)
        {
            return s switch
            {
                IMethodSymbol methodSymbol => methodSymbol.ExplicitInterfaceImplementations.Any(i => IsAccessible(i.ContainingType)),
                IPropertySymbol propertySymbol => propertySymbol.ExplicitInterfaceImplementations.Any(i => IsAccessible(i.ContainingType)),
                _ => false
            };
        }

        internal class CodeFileBuilderEnumFormatter : AbstractSymbolDisplayVisitor
        {
            private readonly CodeFileTokensBuilder _builder;

            public CodeFileBuilderEnumFormatter(CodeFileTokensBuilder builder) : base(null, SymbolDisplayFormat.FullyQualifiedFormat, false, null, 0, false)
            {
                _builder = builder;
            }

            protected override AbstractSymbolDisplayVisitor MakeNotFirstVisitor(bool inNamespaceOrType = false)
            {
                return this;
            }

            protected override void AddLiteralValue(SpecialType type, object value)
            {
                _builder.Append(ObjectDisplay.FormatPrimitive(value, ObjectDisplayOptions.None), CodeFileTokenKind.Literal);
            }

            protected override void AddExplicitlyCastedLiteralValue(INamedTypeSymbol namedType, SpecialType type, object value)
            {
                _builder.Append(ObjectDisplay.FormatPrimitive(value, ObjectDisplayOptions.None), CodeFileTokenKind.Literal);
            }

            protected override void AddSpace()
            {
                _builder.Space();
            }

            protected override void AddBitwiseOr()
            {
                _builder.Punctuation(SyntaxKind.BarToken);
            }

            public override void VisitField(IFieldSymbol symbol)
            {
                _builder.Append(symbol.Type.Name, CodeFileTokenKind.TypeName);
                _builder.Punctuation(SyntaxKind.DotToken);
                _builder.Append(symbol.Name, CodeFileTokenKind.MemberName);
            }

            public void Format(ITypeSymbol type, object typedConstantValue)
            {
                AddNonNullConstantValue(type, typedConstantValue);
            }
        }
    }
}
