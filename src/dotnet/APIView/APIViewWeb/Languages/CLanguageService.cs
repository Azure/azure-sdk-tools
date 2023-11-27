// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ApiView;
using APIView;

namespace APIViewWeb
{
    public class CLanguageService : LanguageService
    {
        private const string CurrentVersion = "5";
        private static Regex _typeTokenizer = new Regex("\\w+|[^\\w]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static HashSet<string> _keywords = new HashSet<string>()
        {
            "auto",
            "break",
            "bool",
            "case",
            "char",
            "const",
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

        private static Regex _packageNameParser = new Regex("([A-Za-z_]*)", RegexOptions.Compiled);

        public override string Name { get; } = "C";

        public override string [] Extensions { get; } = { ".zip" };

        public override bool CanUpdate(string versionString) => versionString != CurrentVersion;

        public override async Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis)
        {
            MemoryStream astStream = new MemoryStream();
            await stream.CopyToAsync(astStream);
            astStream.Position = 0;

            var archive = new ZipArchive(astStream);

            //Generate pacakge name from original file name
            string packageNamespace = "";
            var packageNameMatch = _packageNameParser.Match(originalName);
            if (packageNameMatch.Success)
            {
                packageNamespace = packageNameMatch.Groups[1].Value.Replace("_", "-");
            }

            CodeFileTokensBuilder builder = new CodeFileTokensBuilder();
            List<NavigationItem> navigation = new List<NavigationItem>();

            foreach (var entry in archive.Entries)
            {
                var entryStream = new MemoryStream();
                await entry.Open().CopyToAsync(entryStream);
                BuildNodes(builder, navigation, entryStream);
            }

            return new CodeFile()
            {
                Name = originalName,
                Language = "C",
                Tokens = builder.Tokens.ToArray(),
                Navigation = navigation.ToArray(),
                VersionString = CurrentVersion,
                PackageName = packageNamespace
            };
        }

        private static void BuildNodes(CodeFileTokensBuilder builder, List<NavigationItem> navigation, MemoryStream astStream)
        {
            Span<byte> ast = astStream.ToArray();

            while (ast.Length > 2)
            {
                Utf8JsonReader reader = new Utf8JsonReader(ast);

                var astNode = JsonSerializer.Deserialize<CAstNode>(ref reader);
                ast = ast.Slice((int)reader.BytesConsumed);

                var queue = new Queue<CAstNode>(astNode.inner);
                var types = new HashSet<string>();

                foreach (var node in queue)
                {

                    if (node.kind == "TypedefDecl")
                    {
                        types.Add(node.name);
                    }
                }

                NavigationItem currentFileItem = null;
                List<NavigationItem> currentFileMembers = new List<NavigationItem>();

                while (queue.TryDequeue(out var node))
                {
                    if (node.isImplicit == true || node.loc?.includedFrom?.file != null ||
                        node.loc?.spellingLoc?.includedFrom?.file != null)
                    {
                        continue;
                    }

                    var file = node.loc.file;
                    if (file != null && currentFileItem == null)
                    {
                        currentFileItem = new NavigationItem()
                        {
                            NavigationId = file,
                            Text = file,
                            Tags = { { "TypeKind", "namespace" } }
                        };

                        builder.Append(new CodeFileToken()
                        {
                            DefinitionId = file,
                            Value = "// " + file,
                            Kind = CodeFileTokenKind.Comment,
                        });
                        builder.NewLine();
                        builder.Space();
                        builder.NewLine();
                    }

                    bool TryDequeTypeDef(out CAstNode typedefNode)
                    {
                        if (queue.TryPeek(out typedefNode))
                        {
                            if (typedefNode.kind == "TypedefDecl")
                            {
                                queue.Dequeue();

                                return true;
                            }
                        }

                        typedefNode = null;
                        return false;
                    }

                    void BuildDeclaration(string name, string kind)
                    {
                        builder.Append(new CodeFileToken()
                        {
                            DefinitionId = name,
                            Kind = CodeFileTokenKind.TypeName,
                            Value = name,
                        });
                        currentFileMembers.Add(new NavigationItem()
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

                    switch (node.kind)
                    {
                        case "FunctionDecl":
                            {
                                var type = node.type.qualType;
                                var returnType = type.Split(" ")[0];

                                BuildType(builder, returnType, types);
                                builder.Space();
                                BuildDeclaration(node.name, "method");
                                builder.Punctuation("(");
                                builder.IncrementIndent();

                                bool first = true;
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

                                builder.DecrementIndent();

                                builder.Punctuation(");");
                                builder.NewLine();
                                break;
                            }
                        case "EnumDecl":
                            {
                                if (TryDequeTypeDef(out var typeDef))
                                {
                                    builder.Keyword("typedef");
                                    builder.Space();
                                }

                                builder.Keyword("enum");
                                builder.NewLine();

                                builder.Punctuation("{");
                                builder.NewLine();
                                builder.IncrementIndent();

                                foreach (var parameterNode in node.inner)
                                {
                                    if (parameterNode.kind == "EnumConstantDecl")
                                    {
                                        builder.WriteIndent();
                                        BuildMemberDeclaration(typeDef?.name, parameterNode.name);
                                        if (parameterNode.inner?.FirstOrDefault(n => n.kind == "ConstantExpr") is CAstNode
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

                                if (typeDef != null)
                                {
                                    builder.Space();
                                    BuildDeclaration(typeDef.name, "enum");
                                }

                                builder.Punctuation(";");
                                builder.NewLine();
                                break;
                            }
                        case "TypedefDecl":
                            {
                                builder.Keyword("typedef ");
                                foreach (var typeDefValueNode in node.inner)
                                {
                                    var type = typeDefValueNode.type?.qualType;
                                    if (type != null)
                                    {
                                        BuildType(builder, type, types);
                                    }
                                }

                                builder.Space();
                                BuildDeclaration(node.name, "class");
                                builder.Punctuation(";");
                                builder.NewLine();
                                break;
                            }
                        case "VarDecl":
                            {
                                BuildType(builder, node.type.qualType, types);
                                builder.Space();
                                BuildDeclaration(node.name, "unknown");
                                builder.Punctuation(";");
                                builder.NewLine();
                                break;
                            }
                        case "RecordDecl":
                            {
                                if (TryDequeTypeDef(out var typeDef))
                                {
                                    builder.Keyword("typedef");
                                    builder.Space();
                                }

                                builder.Keyword("struct");
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
                                            BuildMemberDeclaration(typeDef?.name, parameterNode.name);
                                            builder.Punctuation(",");
                                            builder.NewLine();
                                        }
                                    }
                                }

                                builder.DecrementIndent();
                                builder.Punctuation("}");
                                if (typeDef != null)
                                {
                                    builder.Space();
                                    BuildDeclaration(typeDef.name, "struct");
                                }

                                builder.Punctuation(";");
                                builder.NewLine();
                                break;
                            }
                        default:
                            builder.Text(node.ToString());
                            break;
                    }

                    builder.Space();
                    builder.NewLine();
                }

                if (currentFileItem != null)
                {
                    currentFileItem.ChildItems = currentFileMembers.ToArray();
                    navigation.Add(currentFileItem);
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

        private static void BuildExpression(CodeFileTokensBuilder builder, CAstNode exprNode)
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
                case "BinaryOperator":
                    BuildExpression(builder, exprNode.inner[0]);
                    builder.Text($" {exprNode.opcode} ");
                    BuildExpression(builder, exprNode.inner[1]);
                    break;
                case "UnaryOperator":
                    builder.Text(exprNode.opcode);
                    BuildExpression(builder, exprNode.inner[0]);
                    break;
                case "IntegerLiteral":
                    builder.Text(exprNode.value);
                    break;
                case "DeclRefExpr":
                    builder.Text(exprNode.referencedDecl?.name);
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

        private class CAstNode
        {
            public string id { get; set; }
            public string kind { get; set; }
            public bool? isImplicit { get; set; }
            public bool? isReferenced { get; set; }
            public CAstNodeLocation loc { get; set; }
            public CAstNodeType type { get; set; }
            public string name { get; set; }
            public string text { get; set; }
            public string opcode { get; set; }
            public string value { get; set; }
            public CAstNode[] inner { get; set; }
            public CAstNode referencedDecl { get; set; }

            public override string ToString()
            {
                return $"{nameof(kind)}: {kind} {nameof(name)}: {name}";
            }
        }
    }
}
