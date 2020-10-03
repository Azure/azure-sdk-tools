// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ApiView;
using APIView;

namespace APIViewWeb
{
    public class CppLanguageService : LanguageService
    {
        private const string CurrentVersion = "1";
        private static Regex _typeTokenizer = new Regex("\\w+|[^\\w]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static HashSet<string> _keywords = new HashSet<string>()
        {
            "auto",
            "break",
            "bool",
            "case",
            "char",
            "class",
            "const",
            "constexpr",
            "continue",
            "default",
            "do",
            "double",
            "else",
            "enum",
            "extern",
            "float",
            "for",
            "goto",
            "if",
            "inline",
            "int",
            "long",
            "private",
            "protected",
            "public",
            "register",
            "restrict",
            "return",
            "short",
            "signed",
            "sizeof",
            "static",
            "struct",
            "switch",
            "typedef",
            "union",
            "unsigned",
            "virtual",
            "void",
            "volatile",
            "while",
            "_Alignas",
            "_Alignof",
            "_Atomic",
            "_Bool",
            "_Complex",
            "_Generic",
            "_Imaginary",
            "_Noreturn",
            "_Static_assert",
            "_Thread_local"
        };

        public override string Name { get; } = "C++";

        public override string Extension { get; } = ".ast";

        public override bool CanUpdate(string versionString) => versionString != CurrentVersion;

        public override async Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis)
        {
            MemoryStream astStream = new MemoryStream();
            await stream.CopyToAsync(astStream);
            astStream.Position = 0;


            CodeFileTokensBuilder builder = new CodeFileTokensBuilder();
            List<NavigationItem> navigation = new List<NavigationItem>();
            BuildNodes(builder, navigation, astStream);

            return new CodeFile()
            {
                Name = originalName,
                Language = "C++",
                Tokens = builder.Tokens.ToArray(),
                Navigation = navigation.ToArray(),
                VersionString = CurrentVersion,
            };
        }

        private static void BuildNodes(CodeFileTokensBuilder builder, List<NavigationItem> navigation, MemoryStream astStream)
        {
            Span<byte> ast = astStream.ToArray();

            while (ast.Length > 2)
            {
                Utf8JsonReader reader = new Utf8JsonReader(ast);

                var astNode = JsonSerializer.Deserialize<CppAstNode>(ref reader);
                ast = ast.Slice((int)reader.BytesConsumed);

                var queue = new Queue<CppAstNode>(astNode.inner);
                var types = new HashSet<string>();

                NavigationItem currentNamespace = null;
                List<NavigationItem> currentNamespaceMembers = new List<NavigationItem>();

                while (queue.TryDequeue(out var node))
                {
                    if (node.isImplicit == true)
                    {
                        continue;
                    }

                    ProcessNode(node);                    
                    builder.Space();
                    builder.NewLine();
                }

                if (currentNamespace != null)
                {
                    currentNamespace.ChildItems = currentNamespaceMembers.ToArray();
                    navigation.Add(currentNamespace);
                }

                void BuildDeclaration(string name, string kind)
                {
                    builder.Append(new CodeFileToken()
                    {
                        DefinitionId = name,
                        Kind = CodeFileTokenKind.TypeName,
                        Value = name,
                    });
                    currentNamespaceMembers.Add(new NavigationItem()
                    {
                        NavigationId = name,
                        Text = name,
                        Tags = { { "TypeKind", kind } }
                    });
                }

                void BuildMemberDeclaration(string containerName, string name)
                {
                    builder.Append(new CodeFileToken()
                    {
                        DefinitionId = containerName + "." + name,
                        Kind = CodeFileTokenKind.MemberName,
                        Value = name,
                    });
                }

                void ProcessVarDecNode(CppAstNode node)
                {
                    var typeBldr = new StringBuilder();
                    if (node.constexpr == true)
                    {
                        typeBldr.Append("constexpr ");
                    }

                    if (!string.IsNullOrEmpty(node.storageClass))
                    {
                        typeBldr.Append("static ");
                    }
                    typeBldr.Append(node.type.qualType);
                    BuildType(builder, typeBldr.ToString(), types);
                    builder.Space();
                    BuildDeclaration(node.name, "unknown");
                    //todo Some var declaration will have rvalue. Process these sepcial types too.
                    builder.Punctuation(";");
                    builder.NewLine();
                }

                void ProcessCXXRecordDecl(CppAstNode node)
                {
                    if (node.tagUsed == "struct")
                        ProcessStructNode(node);
                    else if (node.tagUsed == "class")
                        ProcessClassNode(node);
                }

                void ProcessClassNode(CppAstNode node)
                {
                    builder.Keyword("class");
                    builder.Space();
                    BuildDeclaration(node.name, "class");
                    builder.Space();
                    //Todo handle inheritance here
                    builder.Punctuation("{");
                    builder.NewLine();
                    builder.IncrementIndent();

                    if (node.inner != null)
                    {
                        foreach (var parameterNode in node.inner)
                        {
                            if (parameterNode.kind == "CXXRecordDecl" && parameterNode.name == node.name)
                                continue;

                            // add public , private or protected access specifier
                            if (parameterNode.kind == "AccessSpecDecl")
                            {
                                builder.DecrementIndent();
                                builder.Keyword(parameterNode.access);
                                builder.Punctuation(":");
                                builder.NewLine();
                                builder.IncrementIndent();
                            }
                            else
                            {
                                ProcessNode(parameterNode);
                            }
                        }
                    }                    

                    builder.DecrementIndent();
                    builder.Punctuation("}");
                    builder.Punctuation(";");
                    builder.NewLine();
                }

                void ProcessStructNode(CppAstNode node)
                {            
                    if (node.name == "ListFileSystemsSegmentOptions")
                    {
                        builder.NewLine();
                    }
                    builder.Keyword("struct");
                    builder.Space();
                    BuildDeclaration(node.name, "struct");
                    builder.NewLine();
                    builder.Punctuation("{");
                    builder.NewLine();
                    builder.IncrementIndent();
                    if (node.inner != null)
                    {
                        foreach (var parameterNode in node.inner)
                        {
                            if (parameterNode.kind == "FieldDecl")
                            {
                                builder.WriteIndent();
                                BuildType(builder, parameterNode.type.qualType, types);
                                builder.Space();
                                // todo:Should pass namspace from qualtype
                                BuildMemberDeclaration("", parameterNode.name);
                                builder.Punctuation(",");
                                builder.NewLine();
                            }
                        }
                    }

                    builder.DecrementIndent();
                    builder.Punctuation("}");
                    builder.Punctuation(";");
                    builder.NewLine();
                }

                void ProcessEnumNode(CppAstNode node)
                {
                    builder.Keyword("enum");
                    builder.Space();
                    if (!string.IsNullOrEmpty(node.scopedEnumTag))
                    {
                        builder.Keyword(node.scopedEnumTag);
                        builder.Space();
                    }

                    if (!string.IsNullOrEmpty(node.name))
                    {
                        BuildDeclaration(node.name, "enum");
                    }
                    builder.NewLine();

                    builder.Punctuation("{");
                    builder.NewLine();
                    builder.IncrementIndent();

                    foreach (var parameterNode in node.inner)
                    {
                        if (parameterNode.kind == "EnumConstantDecl")
                        {
                            builder.WriteIndent();
                            BuildMemberDeclaration("", parameterNode.name);
                            if (parameterNode.inner?.FirstOrDefault(n => n.kind == "ConstantExpr") is CppAstNode
                                exprNode)
                            {
                                builder.Space();
                                builder.Punctuation("=");
                                builder.Space();
                                BuildExpression(builder, exprNode);
                            }

                            builder.Punctuation(",");
                            builder.NewLine();
                        }
                    }

                    builder.DecrementIndent();
                    builder.Punctuation("}");
                    builder.Punctuation(";");
                    builder.NewLine();
                }

                void ProcessFunctionDeclNode(CppAstNode node)
                {
                    if (node.inline == true)
                    {
                        builder.Keyword("inline");
                    }

                    //Todo : add static for static method
                    var type = node.type.qualType;
                    var returnType = type.Split(" ")[0];

                    BuildType(builder, returnType, types);
                    builder.Space();
                    BuildDeclaration(node.name, "method");
                    builder.Punctuation("(");
                    builder.IncrementIndent();

                    bool first = true;
                    if (node.inner != null)
                    {
                        foreach (var parameterNode in node.inner)
                        {
                            if (parameterNode.kind == "ParmVarDecl")
                            {
                                if (first)
                                {
                                    builder.NewLine();
                                    first = false;
                                }

                                builder.WriteIndent();
                                BuildType(builder, parameterNode.type.qualType, types);
                                builder.Space();
                                builder.Text(parameterNode.name);
                                builder.Punctuation(",");
                                builder.NewLine();
                            }
                        }
                    }
                    
                    builder.DecrementIndent();
                    builder.Punctuation(");");
                    builder.NewLine();
                }

                void ProcessConstructorDecl(CppAstNode node)
                {

                }

                void ProcessFieldDecl(CppAstNode node)
                {

                }

                void ProcessNamespaceNoe(CppAstNode node)
                {
                    //Generate namespace name
                    int namespaceDepth = 0;
                    var namespacebldr = new StringBuilder();
                    var parentNode = node;

                    //sampe display of namspace text for C++ is
                    // namespace Azure { namespace Storage { namespace Blobs {
                    // }}}
                    while (node?.kind == "NamespaceDecl")
                    {
                        builder.Keyword("namespace");
                        builder.Space();
                        builder.Text(node.name);
                        builder.Space();
                        builder.Punctuation("{");
                        namespaceDepth++;
                        builder.Space();

                        if (namespacebldr.Length > 0)
                        {
                            namespacebldr.Append("::");
                        }
                        namespacebldr.Append(node.name);

                        parentNode = node;
                        node = node.inner?.FirstOrDefault(n => n.kind == "NamespaceDecl");
                    }                    

                    var name_space = namespacebldr.ToString();
                    types.Add(name_space);
                    currentNamespace = new NavigationItem()
                    {
                        NavigationId = name_space,
                        Text = name_space,
                        Tags = { { "TypeKind", "namespace" } }
                    };

                    //Process all nodesin namespace
                    builder.IncrementIndent();
                    builder.NewLine();                    
                    foreach (var inner in parentNode.inner)
                    {
                        ProcessNode(inner);
                    }
                    builder.DecrementIndent();
                    builder.NewLine();
                    // Mark this namespace as processed by poping last sub namespace
                    while (namespaceDepth > 0)
                    {
                        builder.Punctuation("}");
                        namespaceDepth--;
                    }
                    builder.NewLine();
                }

                void ProcessTypeAlias(CppAstNode node)
                {
                    builder.Keyword("using");
                    builder.Space();
                    builder.Text(node.name);
                    builder.NewLine();
                }

                void ProcessNode(CppAstNode node)
                {
                    switch (node.kind)
                    {
                        case "NamespaceDecl":
                            {
                                ProcessNamespaceNoe(node);
                                break;
                            }

                        case "CXXRecordDecl":
                            {
                                ProcessCXXRecordDecl(node);
                                break;
                            }

                        case "CXXConstructorDecl":
                            {
                                ProcessConstructorDecl(node);
                                break;
                            }

                        case "FunctionDecl":
                        case "CXXMethodDecl":
                            {
                                ProcessFunctionDeclNode(node);
                                break;
                            }
                        case "EnumDecl":
                            {
                                ProcessEnumNode(node);
                                break;
                            }

                        case "FieldDecl":
                            {
                                ProcessFieldDecl(node);
                                break;
                            }

                        case "VarDecl":
                            {
                                ProcessVarDecNode(node);
                                break;
                            }

                        case "TypeAliasDecl":
                            {
                                ProcessTypeAlias(node);
                                break;
                            }

                        default:
                            builder.Text(node.ToString());
                            break;
                    }
                }
            }
        }

        private static void BuildType(CodeFileTokensBuilder builder, string type, HashSet<string> types)
        {
            foreach (Match typePartMatch in _typeTokenizer.Matches(type))
            {
                var typePart = typePartMatch.ToString();
                if (_keywords.Contains(typePart))
                {
                    builder.Keyword(typePart);
                }
                else if (types.Contains(typePart))
                {
                    builder.Append(new CodeFileToken()
                    {
                        Kind = CodeFileTokenKind.TypeName,
                        NavigateToId = typePart,
                        Value = typePart
                    });
                }
                else
                {
                    builder.Text(typePart);
                }
            }
        }

        private static void BuildExpression(CodeFileTokensBuilder builder, CppAstNode exprNode)
        {
            switch (exprNode.kind)
            {
                case "CStyleCastExpr":
                case "ConstantExpr":
                    foreach (var node in exprNode.inner)
                    {
                        BuildExpression(builder, node);
                    }
                    break;
                case "ParenExpr":
                    builder.Punctuation("(");
                    foreach (var node in exprNode.inner)
                    {
                        BuildExpression(builder, node);
                    }
                    builder.Punctuation(")");
                    break;
                case "IntegerLiteral":
                    builder.Text(exprNode.value);
                    break;
                case "DeclRefExpr":
                    builder.Text(exprNode.referencedDecl?.name);
                    break;
                case "StringLiteral":
                    builder.Append(exprNode.value, CodeFileTokenKind.StringLiteral);
                    break;
                case "CXXTemporaryObjectExpr":
                    var subTypes = exprNode.type.qualType.Split("::");
                    var shortType = subTypes[subTypes.Length - 1];
                    builder.Text(shortType);
                    builder.Punctuation("(");
                    builder.Punctuation(")");
                    break;
                default:
                    builder.Text(exprNode + " " + exprNode.value);
                    break;
            }
        }
        private class CAstNodeType
        {
            public string desugaredQualType { get; set; }
            public string qualType { get; set; }
        }

        private class CAstNodeRange
        {
            public CAstNodeLocation begin { get; set; }
            public CAstNodeLocation end { get; set; }
        }

        private class CAstNodeIncludeLocation
        {
            public string file { get; set; }
        }

        private class CAstNodeLocation
        {
            public string file { get; set; }
            public int? offset { get; set; }
            public int? line { get; set; }
            public int? col { get; set; }
            public int? tokLen { get; set; }

            public CAstNodeLocation spellingLoc { get; set; }
            public CAstNodeLocation expansionLoc { get; set; }
            public CAstNodeIncludeLocation includedFrom { get; set; }
        }

        private class CppAstNode
        {
            public string id { get; set; }
            public string kind { get; set; }
            public bool? isImplicit { get; set; }
            public bool? isReferenced { get; set; }
            public CAstNodeLocation loc { get; set; }
            public CAstNodeType type { get; set; }
            public string name { get; set; }
            public string text { get; set; }
            public string value { get; set; }
            public CppAstNode[] inner { get; set; }
            public CppAstNode referencedDecl { get; set; }
            public string storageClass { get; set; }
            public bool? constexpr { get; set; }
            public string mangledName { get; set; }
            public string init { get; set; }
            public bool? inline { get; set; }
            public string valueCategory { get; set; }
            public string castKind { get; set; }
            public string tagUsed { get; set; }
            public string scopedEnumTag { get; set; }
            public string access { get; set; }
            public override string ToString()
            {
                return $"{nameof(kind)}: {kind} {nameof(name)}: {name}";
            }
        }
    }
}