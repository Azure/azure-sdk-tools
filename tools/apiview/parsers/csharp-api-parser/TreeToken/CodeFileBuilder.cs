// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using ApiView;
using APIView.Analysis;
using APIView.TreeToken;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.SymbolDisplay;
using System.Collections.Immutable;
using System.ComponentModel;

namespace CSharpAPIParser.TreeToken
{
    internal class CodeFileBuilder
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

        private IAssemblySymbol? _assembly;

        public ICodeFileBuilderSymbolOrderProvider SymbolOrderProvider { get; set; } = new CodeFileBuilderSymbolOrderProvider();

        public const string CurrentVersion = "27";

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

        public CodeFile Build(IAssemblySymbol assemblySymbol, bool runAnalysis, List<DependencyInfo>? dependencies)
        {
            _assembly = assemblySymbol;
            var analyzer = new Analyzer();

            if (runAnalysis)
            {
                analyzer.VisitAssembly(assemblySymbol);
            }

            var apiTreeNode = new APITreeNode();
            apiTreeNode.Kind = "Assembly";
            apiTreeNode.Id = assemblySymbol.Name;
            apiTreeNode.Name = assemblySymbol.Name + ".dll";
            
            if (dependencies != null)
            {
                BuildDependencies(apiTreeNode.ChildrenObj, dependencies);
            }
            BuildInternalsVisibleToAttributes(apiTreeNode.ChildrenObj, assemblySymbol);

            foreach (var namespaceSymbol in SymbolOrderProvider.OrderNamespaces(EnumerateNamespaces(assemblySymbol)))
            {
                if (namespaceSymbol.IsGlobalNamespace)
                {
                    foreach (var namedTypeSymbol in SymbolOrderProvider.OrderTypes(namespaceSymbol.GetTypeMembers()))
                    {
                        BuildType(apiTreeNode.ChildrenObj, namedTypeSymbol, false);
                    }
                }
                else
                {
                    BuildNamespace(apiTreeNode.ChildrenObj, namespaceSymbol);
                }
            }

            // Sort API Tree by name
            apiTreeNode.SortChildren();

            var treeTokenCodeFile = new CodeFile()
            {
                Name = $"{assemblySymbol.Name} ({assemblySymbol.Identity.Version})",
                Language = "C#",
                APIForest = new List<APITreeNode>() { apiTreeNode },
                VersionString = CurrentVersion,
                Diagnostics = analyzer.Results.ToArray(),
                PackageName = assemblySymbol.Name,
                PackageVersion = assemblySymbol.Identity.Version.ToString()
            };

            return treeTokenCodeFile;
        }

        public static void BuildInternalsVisibleToAttributes(List<APITreeNode> apiTree, IAssemblySymbol assemblySymbol)
        {
            var assemblyAttributes = assemblySymbol.GetAttributes()
                .Where(a =>
                    a.AttributeClass?.Name == "InternalsVisibleToAttribute" &&
                    !a.ConstructorArguments[0].Value?.ToString()?.Contains(".Tests") == true &&
                    !a.ConstructorArguments[0].Value?.ToString()?.Contains(".Perf") == true &&
                    !a.ConstructorArguments[0].Value?.ToString()?.Contains("DynamicProxyGenAssembly2") == true);
            if (assemblyAttributes != null && assemblyAttributes.Any())
            {
                var apiTreeNode = new APITreeNode();
                apiTreeNode.Kind = apiTreeNode.Name = apiTreeNode.Id = "InternalsVisibleTo";
                apiTreeNode.TopTokensObj.Add(StructuredToken.CreateTextToken(value: "Exposes internals to:"));
                apiTreeNode.TopTokensObj.Add(StructuredToken.CreateLineBreakToken());

                foreach (AttributeData attribute in assemblyAttributes)
                {
                    if (attribute.ConstructorArguments.Length > 0)
                    {
                        var param = attribute.ConstructorArguments[0].Value?.ToString();
                        if (!String.IsNullOrEmpty(param))
                        {
                            var firstComma = param?.IndexOf(',');
                            param = firstComma > 0 ? param?[..(int)firstComma] : param;
                            apiTreeNode.TopTokensObj.Add(StructuredToken.CreateTextToken(value: param, id: attribute.AttributeClass?.Name));
                        }
                    }
                }
                apiTreeNode.BottomTokensObj.Add(StructuredToken.CreateEmptyToken());
                apiTreeNode.BottomTokensObj.Add(StructuredToken.CreateLineBreakToken());
                apiTree.Add(apiTreeNode);
            }
        }

        public static void BuildDependencies(List<APITreeNode> apiTree, List<DependencyInfo> dependencies)
        {
            if (dependencies != null && dependencies.Any())
            {
                var apiTreeNode = new APITreeNode();
                apiTreeNode.Kind = apiTreeNode.Name = apiTreeNode.Id = "Dependencies";

                apiTreeNode.TopTokensObj.Add(StructuredToken.CreateLineBreakToken());
                apiTreeNode.TopTokensObj.Add(StructuredToken.CreateTextToken(value: "Dependencies:"));
                apiTreeNode.TopTokensObj.Add(StructuredToken.CreateLineBreakToken());

                foreach (DependencyInfo dependency in dependencies)
                {
                    apiTreeNode.TopTokensObj.Add(StructuredToken.CreateTextToken(value: dependency.Name, id: dependency.Name));
                    apiTreeNode.TopTokensObj.Add(StructuredToken.CreateTextToken(value: $"-{dependency.Version}"));
                    apiTreeNode.TopTokensObj.Add(StructuredToken.CreateLineBreakToken());
                }
                apiTreeNode.BottomTokensObj.Add(StructuredToken.CreateEmptyToken());
                apiTreeNode.BottomTokensObj.Add(StructuredToken.CreateLineBreakToken());
                apiTree.Add(apiTreeNode);
            }
        }

        private void BuildNamespace(List<APITreeNode> apiTree, INamespaceSymbol namespaceSymbol)
        {
            bool isHidden = HasOnlyHiddenTypes(namespaceSymbol);

            var apiTreeNode = new APITreeNode();
            apiTreeNode.Id = namespaceSymbol.GetId();
            apiTreeNode.Name = namespaceSymbol.ToDisplayString();
            apiTreeNode.Kind = "Namespace";

            if (isHidden)
            {
                apiTreeNode.TagsObj.Add("Hidden");
            }
            
            apiTreeNode.TopTokensObj.Add(StructuredToken.CreateKeywordToken(SyntaxKind.NamespaceKeyword));
            apiTreeNode.TopTokensObj.Add(StructuredToken.CreateSpaceToken());
            BuildNamespaceName(apiTreeNode.TopTokensObj, namespaceSymbol);
            apiTreeNode.TopTokensObj.Add(StructuredToken.CreateSpaceToken());
            apiTreeNode.TopTokensObj.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.OpenBraceToken));

            foreach (var namedTypeSymbol in SymbolOrderProvider.OrderTypes(namespaceSymbol.GetTypeMembers()))
            {
                BuildType(apiTreeNode.ChildrenObj, namedTypeSymbol, isHidden);
            }

            apiTreeNode.BottomTokensObj.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.CloseBraceToken));
            apiTreeNode.BottomTokensObj.Add(StructuredToken.CreateLineBreakToken());
            apiTreeNode.BottomTokensObj.Add(StructuredToken.CreateEmptyToken());

            apiTree.Add(apiTreeNode);
        }

        private void BuildNamespaceName(List<StructuredToken> tokenList, INamespaceSymbol namespaceSymbol)
        {
            if (!namespaceSymbol.ContainingNamespace.IsGlobalNamespace)
            {
                BuildNamespaceName(tokenList, namespaceSymbol.ContainingNamespace);
                tokenList.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.DotToken));
            }
            DisplayName(tokenList, namespaceSymbol, namespaceSymbol);
        }


        private bool HasAnyPublicTypes(INamespaceSymbol subNamespaceSymbol)
        {
            return subNamespaceSymbol.GetTypeMembers().Any(IsAccessible);
        }

        private bool HasOnlyHiddenTypes(INamespaceSymbol namespaceSymbol)
        {
            return namespaceSymbol.GetTypeMembers().All(t => IsHiddenFromIntellisense(t) || !IsAccessible(t));
        }

        private void BuildType(List<APITreeNode> apiTree, INamedTypeSymbol namedType, bool inHiddenScope)
        {
            if (!IsAccessible(namedType))
            {
                return;
            }

            bool isHidden = IsHiddenFromIntellisense(namedType);
            var apiTreeNode = new APITreeNode();
            apiTreeNode.Kind = "Type";
            apiTreeNode.PropertiesObj.Add("SubKind", namedType.TypeKind.ToString().ToLowerInvariant());
            apiTreeNode.Id = namedType.GetId();
            apiTreeNode.Name = namedType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

            if (isHidden && !inHiddenScope)
            {
                apiTreeNode.TagsObj.Add("Hidden");
            }

            BuildDocumentation(apiTreeNode.TopTokensObj, namedType);
            BuildAttributes(apiTreeNode.TopTokensObj, namedType.GetAttributes());
            BuildVisibility(apiTreeNode.TopTokensObj, namedType);
            apiTreeNode.TopTokensObj.Add(StructuredToken.CreateSpaceToken());

            switch (namedType.TypeKind)
            {
                case TypeKind.Class:
                    BuildClassModifiers(apiTreeNode.TopTokensObj, namedType);
                    apiTreeNode.TopTokensObj.Add(StructuredToken.CreateKeywordToken(SyntaxKind.ClassKeyword));
                    break;
                case TypeKind.Delegate:
                    apiTreeNode.TopTokensObj.Add(StructuredToken.CreateKeywordToken(SyntaxKind.DelegateKeyword));
                    break;
                case TypeKind.Enum:
                    apiTreeNode.TopTokensObj.Add(StructuredToken.CreateKeywordToken(SyntaxKind.EnumKeyword));
                    break;
                case TypeKind.Interface:
                    apiTreeNode.TopTokensObj.Add(StructuredToken.CreateKeywordToken(SyntaxKind.InterfaceKeyword));
                    break;
                case TypeKind.Struct:
                    if (namedType.IsReadOnly)
                    {
                        apiTreeNode.TopTokensObj.Add(StructuredToken.CreateKeywordToken(SyntaxKind.ReadOnlyKeyword));
                        apiTreeNode.TopTokensObj.Add(StructuredToken.CreateSpaceToken());
                    }
                    apiTreeNode.TopTokensObj.Add(StructuredToken.CreateKeywordToken(SyntaxKind.StructKeyword));
                    break;
            }

            apiTreeNode.TopTokensObj.Add(StructuredToken.CreateSpaceToken());
            DisplayName(apiTreeNode.TopTokensObj, namedType, namedType);

            if (namedType.TypeKind == TypeKind.Delegate)
            {
                apiTreeNode.TopTokensObj.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.SemicolonToken));
                apiTreeNode.TopTokensObj.Add(StructuredToken.CreateLineBreakToken());
                return;
            }

            apiTreeNode.TopTokensObj.Add(StructuredToken.CreateSpaceToken());
            BuildBaseType(apiTreeNode.TopTokensObj, namedType);
            apiTreeNode.TopTokensObj.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.OpenBraceToken));  

            foreach (var namedTypeSymbol in SymbolOrderProvider.OrderTypes(namedType.GetTypeMembers()))
            {
                BuildType(apiTreeNode.ChildrenObj, namedTypeSymbol, inHiddenScope || isHidden);
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
                BuildMember(apiTreeNode.ChildrenObj, member, inHiddenScope);
            }
            apiTreeNode.BottomTokensObj.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.CloseBraceToken));
            apiTreeNode.BottomTokensObj.Add(StructuredToken.CreateLineBreakToken());
            apiTreeNode.BottomTokensObj.Add(StructuredToken.CreateEmptyToken());

            apiTree.Add(apiTreeNode);
        }

        private void BuildDocumentation(List<StructuredToken> tokenList, ISymbol symbol)
        {
            var lines = symbol.GetDocumentationCommentXml()?.Trim().Split(_newlineChars);

            if (lines != null)
            {
                if (lines.All(string.IsNullOrWhiteSpace))
                {
                    return;
                }
                foreach (var line in lines)
                {
                    var docToken = new StructuredToken("// " + line.Trim());
                    docToken.RenderClassesObj.Add("comment");
                    docToken.PropertiesObj.Add("GroupId", "doc");
                    tokenList.Add(docToken);
                    tokenList.Add(StructuredToken.CreateLineBreakToken());
                }
            }
        }

        private static void BuildClassModifiers(List<StructuredToken> tokenList, INamedTypeSymbol namedType)
        {
            if (namedType.IsAbstract)
            {
                tokenList.Add(StructuredToken.CreateKeywordToken(SyntaxKind.AbstractKeyword));
                tokenList.Add(StructuredToken.CreateSpaceToken());
            }

            if (namedType.IsStatic)
            {
                tokenList.Add(StructuredToken.CreateKeywordToken(SyntaxKind.StaticKeyword));
                tokenList.Add(StructuredToken.CreateSpaceToken());
            }

            if (namedType.IsSealed)
            {
                tokenList.Add(StructuredToken.CreateKeywordToken(SyntaxKind.SealedKeyword));
                tokenList.Add(StructuredToken.CreateSpaceToken());
            }
        }

        private void BuildBaseType(List<StructuredToken> tokenList, INamedTypeSymbol namedType)
        {
            bool first = true;

            if (namedType.BaseType != null &&
                namedType.BaseType.SpecialType == SpecialType.None)
            {
                tokenList.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.ColonToken));
                tokenList.Add(StructuredToken.CreateSpaceToken());
                first = false;

                DisplayName(tokenList, namedType.BaseType);
            }

            foreach (var typeInterface in namedType.Interfaces)
            {
                if (!IsAccessible(typeInterface)) continue;

                if (!first)
                {
                    tokenList.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.CommaToken));
                    tokenList.Add(StructuredToken.CreateSpaceToken());
                }
                else
                {
                    tokenList.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.ColonToken));
                    tokenList.Add(StructuredToken.CreateSpaceToken());
                    first = false;
                }

                DisplayName(tokenList, typeInterface);
            }

            if (!first)
            {
                tokenList.Add(StructuredToken.CreateSpaceToken());
            }
        }

        private void BuildMember(List<APITreeNode> apiTree, ISymbol member, bool inHiddenScope)
        {
            bool isHidden = IsHiddenFromIntellisense(member);
            var apiTreeNode = new APITreeNode();
            apiTreeNode.Kind = "Member";
            apiTreeNode.PropertiesObj.Add("SubKind", member.Kind.ToString());
            apiTreeNode.Id = member.GetId();
            apiTreeNode.Name = member.ToDisplayString();
            apiTreeNode.TagsObj.Add("HideFromNav");

            if (isHidden && !inHiddenScope)
            {
                apiTreeNode.TagsObj.Add("Hidden");
            }

            BuildDocumentation(apiTreeNode.TopTokensObj, member);
            BuildAttributes(apiTreeNode.TopTokensObj, member.GetAttributes());
            DisplayName(apiTreeNode.TopTokensObj, member);

            if (member.Kind == SymbolKind.Field && member.ContainingType.TypeKind == TypeKind.Enum)
            {
                apiTreeNode.TopTokensObj.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.CommaToken));
            }
            else if (member.Kind != SymbolKind.Property)
            {
                apiTreeNode.TopTokensObj.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.SemicolonToken));
            }

            apiTreeNode.TopTokensObj.Add(StructuredToken.CreateLineBreakToken());
            apiTree.Add(apiTreeNode);
        }

        private void BuildAttributes(List<StructuredToken> tokenList, ImmutableArray<AttributeData> attributes)
        {
            const string attributeSuffix = "Attribute";
            foreach (var attribute in attributes)
            {
                if (attribute.AttributeClass != null)
                {
                    if ((!IsAccessible(attribute.AttributeClass) &&
                        attribute.AttributeClass.Name != "FriendAttribute" &&
                        attribute.AttributeClass.ContainingNamespace.ToString() != "System.Diagnostics.CodeAnalysis")
                        || IsSkippedAttribute(attribute.AttributeClass))
                    {
                        continue;
                    }

                    if (attribute.AttributeClass.DeclaredAccessibility == Accessibility.Internal || attribute.AttributeClass.DeclaredAccessibility == Accessibility.Friend)
                    {
                        tokenList.Add(StructuredToken.CreateKeywordToken("internal"));
                        tokenList.Add(StructuredToken.CreateSpaceToken());
                    }

                    tokenList.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.OpenBracketToken));
                    var name = attribute.AttributeClass.Name;
                    if (name.EndsWith(attributeSuffix))
                    {
                        name = name.Substring(0, name.Length - attributeSuffix.Length);
                    }
                    tokenList.Add(StructuredToken.CreateTypeNameToken(name));
                    if (attribute.ConstructorArguments.Any())
                    {
                        tokenList.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.OpenParenToken));
                        bool first = true;

                        foreach (var argument in attribute.ConstructorArguments)
                        {
                            if (!first)
                            {
                                tokenList.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.CommaToken));
                                tokenList.Add(StructuredToken.CreateParameterSeparatorToken());
                            }
                            else
                            {
                                first = false;
                            }
                            BuildTypedConstant(tokenList, argument);
                        }

                        foreach (var argument in attribute.NamedArguments)
                        {
                            if (!first)
                            {
                                tokenList.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.CommaToken));
                                tokenList.Add(StructuredToken.CreateParameterSeparatorToken());
                            }
                            else
                            {
                                first = false;
                            }
                            tokenList.Add(StructuredToken.CreateTextToken(argument.Key));
                            tokenList.Add(StructuredToken.CreateSpaceToken());
                            tokenList.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.EqualsToken));
                            tokenList.Add(StructuredToken.CreateSpaceToken());
                            BuildTypedConstant(tokenList, argument.Value);
                        }
                        tokenList.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.CloseParenToken));
                    }
                    tokenList.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.CloseBracketToken));
                    tokenList.Add(StructuredToken.CreateLineBreakToken());
                }
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
                                            && (EditorBrowsableState)d.ConstructorArguments[0].Value! == EditorBrowsableState.Never);

        private bool IsDecoratedWithAttribute(ISymbol member, string attributeName) =>
            member.GetAttributes().Any(d => d.AttributeClass?.Name == attributeName);

        private void BuildTypedConstant(List<StructuredToken> tokenList, TypedConstant typedConstant)
        {
            if (typedConstant.IsNull)
            {
                tokenList.Add(StructuredToken.CreateKeywordToken(SyntaxKind.NullKeyword));
            }
            else if (typedConstant.Kind == TypedConstantKind.Enum)
            {
                new CodeFileBuilderEnumFormatter(tokenList).Format(typedConstant.Type, typedConstant.Value);
            }
            else if (typedConstant.Kind == TypedConstantKind.Type)
            {
                tokenList.Add(StructuredToken.CreateKeywordToken(SyntaxKind.TypeOfKeyword));
                tokenList.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.OpenParenToken));
                DisplayName(tokenList, (ITypeSymbol)typedConstant.Value!);
                tokenList.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.CloseParenToken));
            }
            else if (typedConstant.Kind == TypedConstantKind.Array)
            {
                tokenList.Add(StructuredToken.CreateKeywordToken(SyntaxKind.NewKeyword));
                tokenList.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.OpenBracketToken));
                tokenList.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.CloseBracketToken));
                tokenList.Add(StructuredToken.CreateSpaceToken());
                tokenList.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.OpenBraceToken));

                bool first = true;

                foreach (var value in typedConstant.Values)
                {
                    if (!first)
                    {
                        tokenList.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.CommaToken));
                        tokenList.Add(StructuredToken.CreateSpaceToken());
                    }
                    else
                    {
                        first = false;
                    }

                    BuildTypedConstant(tokenList, value);
                }
                tokenList.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.CloseBraceToken));
            }
            else
            {
                if (typedConstant.Value is string s)
                {
                    tokenList.Add(StructuredToken.CreateStringLiteralToken(ObjectDisplay.FormatLiteral(s, ObjectDisplayOptions.UseQuotes | ObjectDisplayOptions.EscapeNonPrintableCharacters)));
                }
                else
                {
                    tokenList.Add(StructuredToken.CreateLiteralToken(ObjectDisplay.FormatPrimitive(typedConstant.Value, ObjectDisplayOptions.None)));
                }
            }
        }

        private void BuildVisibility(List<StructuredToken> tokenList, ISymbol symbol)
        {
            tokenList.Add(StructuredToken.CreateKeywordToken(ToEffectiveAccessibility(symbol.DeclaredAccessibility)));
        }

        private void DisplayName(List<StructuredToken> tokenList, ISymbol symbol, ISymbol? definedSymbol = null)
        {
            tokenList.Add(StructuredToken.CreateEmptyToken(id: symbol.GetId()));
            if (NeedsAccessibility(symbol))
            {
                tokenList.Add(StructuredToken.CreateKeywordToken(ToEffectiveAccessibility(symbol.DeclaredAccessibility)));
                tokenList.Add(StructuredToken.CreateSpaceToken());
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
                    tokenList.Add(MapToken(definedSymbol!, parts[i]));
                }
            }
            else
            {
                foreach (var symbolDisplayPart in symbol.ToDisplayParts(_defaultDisplayFormat))
                {
                    tokenList.Add(MapToken(definedSymbol!, symbolDisplayPart));
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

        private StructuredToken MapToken(ISymbol definedSymbol, SymbolDisplayPart symbolDisplayPart)
        {
            string? navigateToId = null;
            var symbol = symbolDisplayPart.Symbol;

            if (symbol is INamedTypeSymbol &&
                (definedSymbol == null || !SymbolEqualityComparer.Default.Equals(definedSymbol, symbol)) &&
                SymbolEqualityComparer.Default.Equals(_assembly, symbol.ContainingAssembly))
            {
                navigateToId = symbol.GetId();
            }

            var definitionId = (definedSymbol != null && SymbolEqualityComparer.Default.Equals(definedSymbol, symbol)) ? definedSymbol.GetId() : null;
            var tokenValue = symbolDisplayPart.ToString();

            StructuredToken? token = null;

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
                    token = StructuredToken.CreateTypeNameToken(tokenValue);
                    break;
                case SymbolDisplayPartKind.Keyword:
                    token = StructuredToken.CreateKeywordToken(tokenValue);
                    break;
                case SymbolDisplayPartKind.LineBreak:
                    token = StructuredToken.CreateLineBreakToken();
                    break;
                case SymbolDisplayPartKind.StringLiteral:
                    token = StructuredToken.CreateStringLiteralToken(tokenValue);
                    break;
                case SymbolDisplayPartKind.Punctuation:
                    token = StructuredToken.CreatePunctuationToken(tokenValue);
                    break;
                case SymbolDisplayPartKind.Space:
                    token = StructuredToken.CreateSpaceToken();
                    break;
                case SymbolDisplayPartKind.PropertyName:
                case SymbolDisplayPartKind.EventName:
                case SymbolDisplayPartKind.FieldName:
                case SymbolDisplayPartKind.MethodName:
                case SymbolDisplayPartKind.Operator:
                case SymbolDisplayPartKind.EnumMemberName:
                case SymbolDisplayPartKind.ExtensionMethodName:
                case SymbolDisplayPartKind.ConstantName:
                    token = StructuredToken.CreateMemberNameToken(tokenValue);
                    break;
                default:
                    token = StructuredToken.CreateTextToken(tokenValue);
                    break;
            }

            if (!String.IsNullOrWhiteSpace(definitionId))
            {
                token.Id = definitionId!;
            }

            if (!String.IsNullOrWhiteSpace(navigateToId))
            {
                token.PropertiesObj.Add("NavigateToId", navigateToId!);
            }
            
            return token;
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
            private readonly List<StructuredToken> _tokenList;

            public CodeFileBuilderEnumFormatter(List<StructuredToken> tokenList) : base(null, SymbolDisplayFormat.FullyQualifiedFormat, false, null, 0, false)
            {
                _tokenList = tokenList;
            }

            protected override AbstractSymbolDisplayVisitor MakeNotFirstVisitor(bool inNamespaceOrType = false)
            {
                return this;
            }

            protected override void AddLiteralValue(SpecialType type, object value)
            {
                _tokenList.Add(StructuredToken.CreateLiteralToken(ObjectDisplay.FormatPrimitive(value, ObjectDisplayOptions.None)));
            }

            protected override void AddExplicitlyCastedLiteralValue(INamedTypeSymbol namedType, SpecialType type, object value)
            {
                _tokenList.Add(StructuredToken.CreateLiteralToken(ObjectDisplay.FormatPrimitive(value, ObjectDisplayOptions.None)));
            }

            protected override void AddSpace()
            {
                _tokenList.Add(StructuredToken.CreateSpaceToken());
            }

            protected override void AddBitwiseOr()
            {
                _tokenList.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.BarToken));
            }

            public override void VisitField(IFieldSymbol symbol)
            {
                _tokenList.Add(StructuredToken.CreateTypeNameToken(symbol.Type.Name));
                _tokenList.Add(StructuredToken.CreatePunctuationToken(SyntaxKind.DotToken));
                _tokenList.Add(StructuredToken.CreateMemberNameToken(symbol.Name));
            }

            public void Format(ITypeSymbol? type, object? typedConstantValue)
            {
                AddNonNullConstantValue(type, typedConstantValue);
            }
        }
    }
}
