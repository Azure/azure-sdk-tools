// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIView.Analysis;
using APIView.Model.V2;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.SymbolDisplay;
using System.Collections.Immutable;
using System.ComponentModel;
using ApiView;

namespace CSharpAPIParser.TreeToken
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

        private IAssemblySymbol? _assembly;

        public ICodeFileBuilderSymbolOrderProvider SymbolOrderProvider { get; set; } = new CodeFileBuilderSymbolOrderProvider();

        public const string CurrentVersion = "29.1";

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

            var codeFile = new CodeFile()
            {
                Language = "C#",
                ParserVersion = CurrentVersion,
                PackageName = assemblySymbol.Name,
                PackageVersion = assemblySymbol.Identity.Version.ToString()
            };

            if (dependencies != null)
            {
                BuildDependencies(codeFile.ReviewLines, dependencies);
            }
            BuildInternalsVisibleToAttributes(codeFile.ReviewLines, assemblySymbol);

            foreach (var namespaceSymbol in SymbolOrderProvider.OrderNamespaces(EnumerateNamespaces(assemblySymbol)))
            {
                if (namespaceSymbol.IsGlobalNamespace)
                {
                    foreach (var namedTypeSymbol in SymbolOrderProvider.OrderTypes(namespaceSymbol.GetTypeMembers()))
                    {
                        BuildType(codeFile.ReviewLines, namedTypeSymbol, false);
                    }
                }
                else
                {
                    BuildNamespace(codeFile.ReviewLines, namespaceSymbol);
                }
            }

            codeFile.Diagnostics = analyzer.Results.ToArray();
            return codeFile;
        }

        public static void BuildInternalsVisibleToAttributes(List<ReviewLine> reviewLines, IAssemblySymbol assemblySymbol)
        {
            var assemblyAttributes = assemblySymbol.GetAttributes()
                .Where(a =>
                    a.AttributeClass?.Name == "InternalsVisibleToAttribute" &&
                    !a.ConstructorArguments[0].Value?.ToString()?.Contains(".Tests") == true &&
                    !a.ConstructorArguments[0].Value?.ToString()?.Contains(".Perf") == true &&
                    !a.ConstructorArguments[0].Value?.ToString()?.Contains("DynamicProxyGenAssembly2") == true);
            if (assemblyAttributes != null && assemblyAttributes.Any())
            {
                var internalVisibleLine = new ReviewLine()
                {
                    LineId = "InternalsVisibleTo",
                    Tokens = [
                        ReviewToken.CreateStringLiteralToken("Exposes internals to:")
                    ]
                };
                reviewLines.Add(internalVisibleLine);

                foreach (AttributeData attribute in assemblyAttributes)
                {
                    if (attribute.ConstructorArguments.Length > 0)
                    {
                        var param = attribute.ConstructorArguments[0].Value?.ToString();
                        if (!String.IsNullOrEmpty(param))
                        {
                            var firstComma = param?.IndexOf(',');
                            param = firstComma > 0 ? param?[..(int)firstComma] : param;
                            reviewLines.Add(new ReviewLine()
                            {
                                LineId = attribute.AttributeClass?.Name,
                                Tokens = [
                                    ReviewToken.CreateStringLiteralToken(param)
                                ]
                            });
                        }
                    }
                }
                // Add an empty line after internals visible to section
                reviewLines.Add(new ReviewLine() { RelatedToLine = internalVisibleLine.LineId });
            }
        }

        public static void BuildDependencies(List<ReviewLine> reviewLines, List<DependencyInfo> dependencies)
        {
            if (dependencies != null && dependencies.Any())
            {
                //Dependencies
                var headerLine = new ReviewLine()
                {
                    LineId = "Dependencies"
                };
                var depToken = ReviewToken.CreateStringLiteralToken("Dependencies:");
                depToken.NavigationDisplayName = "Dependencies";
                depToken.RenderClasses.Add("dependencies");
                headerLine.Tokens.Add(depToken);
                reviewLines.Add(headerLine);

                foreach (DependencyInfo dependency in dependencies)
                {
                    var versionToken = ReviewToken.CreateStringLiteralToken($"-{dependency.Version}");
                    versionToken.SkipDiff = true;
                    var dependencyLine = new ReviewLine()
                    {
                        LineId = dependency.Name,
                        Tokens = [
                            ReviewToken.CreateStringLiteralToken(dependency.Name, false),
                            versionToken
                        ]
                    };
                    reviewLines.Add(dependencyLine);
                }
                reviewLines.Add(new ReviewLine() { RelatedToLine = headerLine.LineId });
            }
        }

        private void BuildNamespace(List<ReviewLine> reviewLines, INamespaceSymbol namespaceSymbol)
        {
            bool isHidden = HasOnlyHiddenTypes(namespaceSymbol);
            var namespaceLine = new ReviewLine()
            {
                LineId = namespaceSymbol.GetId(),
                Tokens = [
                    ReviewToken.CreateKeywordToken("namespace")
                ],
                IsHidden = isHidden
            };

            BuildNamespaceName(namespaceLine, namespaceSymbol);
            var nameSpaceToken = namespaceLine.Tokens.LastOrDefault();
            if (nameSpaceToken != null)
            {
                nameSpaceToken.RenderClasses.Add("namespace");
                nameSpaceToken.NavigationDisplayName = namespaceSymbol.ToDisplayString();
            }
            namespaceLine.Tokens.Last().HasSuffixSpace = true;
            namespaceLine.Tokens.Add(ReviewToken.CreatePunctuationToken("{"));

            // Add each members in the namespace
            foreach (var namedTypeSymbol in SymbolOrderProvider.OrderTypes(namespaceSymbol.GetTypeMembers()).OrderBy(s => s.GetId()))
            {
                BuildType(namespaceLine.Children, namedTypeSymbol, isHidden);
            }

            reviewLines.Add(namespaceLine);
            reviewLines.Add(new ReviewLine()
            {
                Tokens = [
                    ReviewToken.CreateStringLiteralToken("}")
                ],
                IsHidden = isHidden,
                IsContextEndLine = true
            });
            //Add an empty line in the review after current name space.
            reviewLines.Add(new ReviewLine() { IsHidden = isHidden, RelatedToLine = namespaceLine.LineId});
        }

        private void BuildNamespaceName(ReviewLine namespaceLine, INamespaceSymbol namespaceSymbol)
        {
            if (!namespaceSymbol.ContainingNamespace.IsGlobalNamespace)
            {
                BuildNamespaceName(namespaceLine, namespaceSymbol.ContainingNamespace);
                var punctuation = ReviewToken.CreatePunctuationToken(".", false);
                namespaceLine.Tokens.Add(punctuation);
            }
            DisplayName(namespaceLine, namespaceSymbol, namespaceSymbol);
        }

        private bool HasAnyPublicTypes(INamespaceSymbol subNamespaceSymbol)
        {
            return subNamespaceSymbol.GetTypeMembers().Any(IsAccessible);
        }

        private bool HasOnlyHiddenTypes(INamespaceSymbol namespaceSymbol)
        {
            return namespaceSymbol.GetTypeMembers().All(t => IsHiddenFromIntellisense(t) || !IsAccessible(t));
        }

        private void BuildType(List<ReviewLine> reviewLines, INamedTypeSymbol namedType, bool inHiddenScope)
        {
            if (!IsAccessible(namedType))
            {
                return;
            }

            bool isHidden = IsHiddenFromIntellisense(namedType) || inHiddenScope;
            var reviewLine = new ReviewLine()
            {
                LineId = namedType.GetId(),
                IsHidden = isHidden
            };

            // Build documentation, attributes, visibility, and name
            BuildDocumentation(reviewLines, namedType, isHidden, namedType.GetId());
            BuildAttributes(reviewLines, namedType.GetAttributes(), isHidden, namedType.GetId());
            BuildVisibility(reviewLine.Tokens, namedType);

            switch (namedType.TypeKind)
            {
                case TypeKind.Class:
                    BuildClassModifiers(reviewLine.Tokens, namedType);
                    reviewLine.Tokens.Add(ReviewToken.CreateKeywordToken(SyntaxKind.ClassKeyword));
                    break;
                case TypeKind.Delegate:
                    reviewLine.Tokens.Add(ReviewToken.CreateKeywordToken(SyntaxKind.DelegateKeyword));
                    break;
                case TypeKind.Enum:
                    reviewLine.Tokens.Add(ReviewToken.CreateKeywordToken(SyntaxKind.EnumKeyword));
                    break;
                case TypeKind.Interface:
                    reviewLine.Tokens.Add(ReviewToken.CreateKeywordToken(SyntaxKind.InterfaceKeyword));
                    break;
                case TypeKind.Struct:
                    if (namedType.IsReadOnly)
                    {
                        reviewLine.Tokens.Add(ReviewToken.CreateKeywordToken(SyntaxKind.ReadOnlyKeyword));
                    }
                    reviewLine.Tokens.Add(ReviewToken.CreateKeywordToken(SyntaxKind.StructKeyword));
                    break;
            }

            DisplayName(reviewLine, namedType, namedType);

            // Add navigation short name and render classes to Type name token. Navigation tree is built dynamically based on these properties
            var typeToken = reviewLine.Tokens.FirstOrDefault(t => t.Kind == TokenKind.TypeName && string.IsNullOrEmpty(t.NavigateToId));
            if (typeToken != null)
            {
                typeToken.NavigationDisplayName = namedType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                typeToken.RenderClasses.Add(namedType.TypeKind.ToString().ToLowerInvariant());
            }
            if (namedType.TypeKind == TypeKind.Delegate)
            {
                reviewLine.Tokens.Last().HasSuffixSpace = false;
                reviewLine.Tokens.Add(ReviewToken.CreatePunctuationToken(SyntaxKind.SemicolonToken));
                reviewLines.Add(reviewLine);
                return;
            }

            reviewLine.Tokens.Last().HasSuffixSpace = true;
            BuildBaseType(reviewLine, namedType);
            reviewLine.Tokens.Add(ReviewToken.CreatePunctuationToken(SyntaxKind.OpenBraceToken));
            foreach (var namedTypeSymbol in SymbolOrderProvider.OrderTypes(namedType.GetTypeMembers()))
            {
                BuildType(reviewLine.Children, namedTypeSymbol, isHidden);
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
                BuildMember(reviewLine.Children, member, isHidden);
            }
            reviewLines.Add(reviewLine);
            reviewLines.Add(new ReviewLine()
            {
                Tokens = [
                    ReviewToken.CreateStringLiteralToken("}")
                ],
                IsHidden = isHidden,
                IsContextEndLine = true
            });
            reviewLines.Add(new ReviewLine() { IsHidden = isHidden, RelatedToLine = reviewLine.LineId });
        }

        private void BuildDocumentation(List<ReviewLine> reviewLines, ISymbol symbol, bool isHidden, string relatedTo)
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
                    var docToken = ReviewToken.CreateCommentToken("// " + line.Trim());
                    docToken.IsDocumentation = true;
                    reviewLines.Add(new ReviewLine()
                    {
                        Tokens = [docToken],
                        IsHidden = isHidden,
                        RelatedToLine = relatedTo
                    });
                }
            }
        }

        private static void BuildClassModifiers(List<ReviewToken> tokenList, INamedTypeSymbol namedType)
        {
            if (namedType.IsAbstract)
            {
                tokenList.Add(ReviewToken.CreateKeywordToken(SyntaxKind.AbstractKeyword));
            }

            if (namedType.IsStatic)
            {
                tokenList.Add(ReviewToken.CreateKeywordToken(SyntaxKind.StaticKeyword));
            }

            if (namedType.IsSealed)
            {
                tokenList.Add(ReviewToken.CreateKeywordToken(SyntaxKind.SealedKeyword));
            }
        }

        private void BuildBaseType(ReviewLine reviewLine, INamedTypeSymbol namedType)
        {
            bool first = true;

            if (namedType.BaseType != null &&
                namedType.BaseType.SpecialType == SpecialType.None)
            {
                reviewLine.AddToken(ReviewToken.CreatePunctuationToken(SyntaxKind.ColonToken));
                first = false;
                DisplayName(reviewLine, namedType.BaseType);
                reviewLine.Tokens.Last().HasSuffixSpace = true;
            }

            foreach (var typeInterface in namedType.Interfaces)
            {
                if (!IsAccessible(typeInterface)) continue;

                if (!first)
                {
                    reviewLine.Tokens.Last().HasSuffixSpace = false;
                    reviewLine.AddToken(ReviewToken.CreatePunctuationToken(SyntaxKind.CommaToken));
                }
                else
                {
                    reviewLine.AddToken(ReviewToken.CreatePunctuationToken(SyntaxKind.ColonToken));
                    first = false;
                }
                DisplayName(reviewLine, typeInterface);
                reviewLine.Tokens.Last().HasSuffixSpace = true;
            }
        }

        private void BuildMember(List<ReviewLine> reviewLines, ISymbol member, bool inHiddenScope)
        {
            bool isHidden = IsHiddenFromIntellisense(member) || inHiddenScope;
            var reviewLine = new ReviewLine()
            {
                LineId = member.GetId(),
                IsHidden = isHidden
            };

            BuildDocumentation(reviewLines, member, isHidden, member.GetId());
            BuildAttributes(reviewLines, member.GetAttributes(), isHidden, member.GetId());
            reviewLines.Add(reviewLine);
            DisplayName(reviewLine, member);
            reviewLine.Tokens.Last().HasSuffixSpace = false;

            // Set member sub kind class for render class styling
            var memToken = reviewLine.Tokens.FirstOrDefault(m => m.Kind == TokenKind.MemberName);
            if (memToken != null)
            {
                memToken.RenderClasses.Add(member.Kind.ToString().ToLowerInvariant());
            }

            if (member.Kind == SymbolKind.Field && member.ContainingType.TypeKind == TypeKind.Enum)
            {
                reviewLine.AddToken(ReviewToken.CreatePunctuationToken(SyntaxKind.CommaToken));
            }
            else if (member.Kind != SymbolKind.Property)
            {
                reviewLine.AddToken(ReviewToken.CreatePunctuationToken(SyntaxKind.SemicolonToken));
            }
        }

        private void BuildAttributes(List<ReviewLine> reviewLines, ImmutableArray<AttributeData> attributes, bool isHidden, string relatedTo)
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

                    var attributeLine = new ReviewLine()
                    {
                        // GetId() is not unique for attribute class. for e.g. attribute class id is something like "System.FlagsAttribute"
                        // So, using a unique id for attribute line
                        LineId = $"{attribute.AttributeClass.GetId()}.{relatedTo}",
                        IsHidden = isHidden
                    };

                    if (attribute.AttributeClass.DeclaredAccessibility == Accessibility.Internal || attribute.AttributeClass.DeclaredAccessibility == Accessibility.Friend)
                    {
                        attributeLine.AddToken(ReviewToken.CreateKeywordToken("internal"));
                    }

                    attributeLine.AddToken(ReviewToken.CreatePunctuationToken(SyntaxKind.OpenBracketToken, false));
                    var name = attribute.AttributeClass.Name;
                    if (name.EndsWith(attributeSuffix))
                    {
                        name = name.Substring(0, name.Length - attributeSuffix.Length);
                    }
                    attributeLine.AddToken(ReviewToken.CreateTypeNameToken(name, false));
                    if (attribute.ConstructorArguments.Any())
                    {
                        attributeLine.AddToken(ReviewToken.CreatePunctuationToken(SyntaxKind.OpenParenToken, false));
                        bool first = true;

                        foreach (var argument in attribute.ConstructorArguments)
                        {
                            if (!first)
                            {
                                attributeLine.AddToken(ReviewToken.CreatePunctuationToken(SyntaxKind.CommaToken));
                            }
                            else
                            {
                                first = false;
                            }
                            BuildTypedConstant(attributeLine, argument);
                        }

                        foreach (var argument in attribute.NamedArguments)
                        {
                            if (!first)
                            {
                                attributeLine.AddToken(ReviewToken.CreatePunctuationToken(SyntaxKind.CommaToken));
                            }
                            else
                            {
                                first = false;
                            }
                            attributeLine.AddToken(ReviewToken.CreateTextToken(argument.Key));
                            attributeLine.AddToken(ReviewToken.CreatePunctuationToken(SyntaxKind.EqualsToken));
                            BuildTypedConstant(attributeLine, argument.Value);
                        }
                        attributeLine.Tokens.Last().HasSuffixSpace = false;
                        attributeLine.AddToken(ReviewToken.CreatePunctuationToken(SyntaxKind.CloseParenToken));
                    }
                    attributeLine.Tokens.Last().HasSuffixSpace = false;
                    attributeLine.AddToken(ReviewToken.CreatePunctuationToken(SyntaxKind.CloseBracketToken));
                    attributeLine.RelatedToLine = relatedTo;
                    //Add current attribute line to review lines
                    reviewLines.Add(attributeLine);
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

        private void BuildTypedConstant(ReviewLine reviewLine, TypedConstant typedConstant)
        {
            var tokenList = reviewLine.Tokens;
            if (typedConstant.IsNull)
            {
                tokenList.Add(ReviewToken.CreateKeywordToken(SyntaxKind.NullKeyword, false));
            }
            else if (typedConstant.Kind == TypedConstantKind.Enum)
            {
                new CodeFileBuilderEnumFormatter(tokenList).Format(typedConstant.Type, typedConstant.Value);
            }
            else if (typedConstant.Kind == TypedConstantKind.Type)
            {
                tokenList.Add(ReviewToken.CreateKeywordToken(SyntaxKind.TypeOfKeyword, false));
                tokenList.Add(ReviewToken.CreatePunctuationToken(SyntaxKind.OpenParenToken, false));
                DisplayName(reviewLine, (ITypeSymbol)typedConstant.Value!);
                reviewLine.Tokens.Last().HasSuffixSpace = false;
                tokenList.Add(ReviewToken.CreatePunctuationToken(SyntaxKind.CloseParenToken, false));
            }
            else if (typedConstant.Kind == TypedConstantKind.Array)
            {
                tokenList.Add(ReviewToken.CreateKeywordToken(SyntaxKind.NewKeyword));
                tokenList.Add(ReviewToken.CreatePunctuationToken(SyntaxKind.OpenBracketToken, false));
                tokenList.Add(ReviewToken.CreatePunctuationToken(SyntaxKind.CloseBracketToken));
                tokenList.Add(ReviewToken.CreatePunctuationToken(SyntaxKind.OpenBraceToken));

                bool first = true;

                foreach (var value in typedConstant.Values)
                {
                    if (!first)
                    {
                        tokenList.Add(ReviewToken.CreatePunctuationToken(SyntaxKind.CommaToken));
                    }
                    else
                    {
                        first = false;
                    }

                    BuildTypedConstant(reviewLine, value);
                    reviewLine.Tokens.Last().HasSuffixSpace = false;
                }
                tokenList.Add(ReviewToken.CreatePunctuationToken(SyntaxKind.CloseBraceToken, false));
            }
            else
            {
                if (typedConstant.Value is string s)
                {
                    tokenList.Add(ReviewToken.CreateStringLiteralToken(ObjectDisplay.FormatLiteral(s, ObjectDisplayOptions.UseQuotes | ObjectDisplayOptions.EscapeNonPrintableCharacters), false));
                }
                else
                {
                    tokenList.Add(ReviewToken.CreateLiteralToken(ObjectDisplay.FormatPrimitive(typedConstant.Value, ObjectDisplayOptions.None), false));
                }
            }
        }

        private void BuildVisibility(List<ReviewToken> tokenList, ISymbol symbol)
        {
            tokenList.Add(ReviewToken.CreateKeywordToken(ToEffectiveAccessibility(symbol.DeclaredAccessibility)));
        }

        private void DisplayName(ReviewLine reviewLine, ISymbol symbol, ISymbol? definedSymbol = null)
        {
            var reviewLineTokens = reviewLine.Tokens;

            if (NeedsAccessibility(symbol))
            {
                reviewLineTokens.Add(ReviewToken.CreateKeywordToken(ToEffectiveAccessibility(symbol.DeclaredAccessibility)));
            }
            if (symbol is IPropertySymbol propSymbol && propSymbol.DeclaredAccessibility != Accessibility.Internal)
            {
                SymbolDisplayPart previous = default(SymbolDisplayPart);
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
                    var previousToken = reviewLine.Tokens.LastOrDefault();
                    //Add a new code line as child if there is a line break
                    if (parts[i].Kind == SymbolDisplayPartKind.LineBreak)
                    {
                        var subLine = new ReviewLine()
                        {
                            LineId = definedSymbol.GetId(),
                        };
                        reviewLine.Children.Add(subLine);
                        reviewLineTokens = subLine.Tokens;
                    }
                    var token = MapToken(definedSymbol: definedSymbol!, symbolDisplayPart: parts[i],
                        previousSymbolDisplayPart: previous, previousToken);
                    if (token != null)
                    {
                        reviewLineTokens.Add(token);
                    }
                    previous = parts[i];
                }
            }
            else
            {
                SymbolDisplayPart previous = default(SymbolDisplayPart);
                foreach (var symbolDisplayPart in symbol.ToDisplayParts(_defaultDisplayFormat))
                {
                    var previousToken = reviewLine.Tokens.LastOrDefault();
                    var token = MapToken(definedSymbol: definedSymbol!, symbolDisplayPart: symbolDisplayPart,
                        previousSymbolDisplayPart: previous, previousToken: previousToken);
                    if (token != null)
                    {
                        reviewLineTokens.Add(token);
                    }
                    previous = symbolDisplayPart;
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

        private ReviewToken? MapToken(ISymbol definedSymbol, SymbolDisplayPart symbolDisplayPart, SymbolDisplayPart previousSymbolDisplayPart, ReviewToken? previousToken)
        {
            string? navigateToId = null;
            var symbol = symbolDisplayPart.Symbol;

            if (symbol is INamedTypeSymbol &&
                (definedSymbol == null || !SymbolEqualityComparer.Default.Equals(definedSymbol, symbol)) &&
                SymbolEqualityComparer.Default.Equals(_assembly, symbol.ContainingAssembly))
            {
                navigateToId = symbol.GetId();
            }

            var tokenValue = symbolDisplayPart.ToString();

            ReviewToken? token = null;

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
                    token = ReviewToken.CreateTypeNameToken(tokenValue, false);
                    break;
                case SymbolDisplayPartKind.Keyword:
                    token = ReviewToken.CreateKeywordToken(tokenValue, false);
                    break;
                case SymbolDisplayPartKind.StringLiteral:
                    token = ReviewToken.CreateStringLiteralToken(tokenValue, false);
                    break;
                case SymbolDisplayPartKind.Punctuation:
                    token = ReviewToken.CreatePunctuationToken(tokenValue, false);
                    break;
                case SymbolDisplayPartKind.Space:
                    if (previousToken != null)
                    {
                        previousToken.HasSuffixSpace = true;
                    }
                    break;
                case SymbolDisplayPartKind.PropertyName:
                case SymbolDisplayPartKind.EventName:
                case SymbolDisplayPartKind.FieldName:
                case SymbolDisplayPartKind.MethodName:
                case SymbolDisplayPartKind.Operator:
                case SymbolDisplayPartKind.EnumMemberName:
                case SymbolDisplayPartKind.ExtensionMethodName:
                case SymbolDisplayPartKind.ConstantName:
                    token = ReviewToken.CreateMemberNameToken(tokenValue, false);
                    break;
                default:
                    token = ReviewToken.CreateTextToken(tokenValue, hasSuffixSpace: false);
                    break;
            }
            if (token != null && !String.IsNullOrWhiteSpace(navigateToId))
            {
                token.NavigateToId = navigateToId!;
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
                    return s.GetAttributes().Any(a => a.AttributeClass?.Name == "FriendAttribute");
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
            private readonly List<ReviewToken> _tokenList;

            public CodeFileBuilderEnumFormatter(List<ReviewToken> tokenList) : base(null, SymbolDisplayFormat.FullyQualifiedFormat, false, null, 0, false)
            {
                _tokenList = tokenList;
            }

            protected override AbstractSymbolDisplayVisitor MakeNotFirstVisitor(bool inNamespaceOrType = false)
            {
                return this;
            }

            protected override void AddLiteralValue(SpecialType type, object value)
            {
                _tokenList.Add(ReviewToken.CreateLiteralToken(ObjectDisplay.FormatPrimitive(value, ObjectDisplayOptions.None)));
            }

            protected override void AddExplicitlyCastedLiteralValue(INamedTypeSymbol namedType, SpecialType type, object value)
            {
                _tokenList.Add(ReviewToken.CreateLiteralToken(ObjectDisplay.FormatPrimitive(value, ObjectDisplayOptions.None)));
            }

            protected override void AddSpace()
            {
                var lastToken = _tokenList.LastOrDefault();
                if (lastToken != null)
                {
                    lastToken.HasSuffixSpace = true;
                }
            }

            protected override void AddBitwiseOr()
            {
                if(_tokenList.Count > 0)
                    _tokenList.Last().HasSuffixSpace = true;
                _tokenList.Add(ReviewToken.CreatePunctuationToken(SyntaxKind.BarToken));
            }

            public override void VisitField(IFieldSymbol symbol)
            {
                _tokenList.Add(ReviewToken.CreateTypeNameToken(symbol.Type.Name, false));
                _tokenList.Add(ReviewToken.CreatePunctuationToken(SyntaxKind.DotToken, false));
                _tokenList.Add(ReviewToken.CreateMemberNameToken(symbol.Name, false));
            }

            public void Format(ITypeSymbol? type, object? typedConstantValue)
            {
                AddNonNullConstantValue(type, typedConstantValue);
            }
        }
    }
}
