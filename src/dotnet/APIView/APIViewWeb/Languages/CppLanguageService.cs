// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ApiView;
using APIView;

namespace APIViewWeb
{
    public class CppLanguageService : LanguageService
    {
        private const string CurrentVersion = "4.0.0";
        private const string NamespaceDeclKind = "NamespaceDecl";
        private const string CxxRecordDeclKind = "CXXRecordDecl";
        private const string CxxMethodDeclKind = "CXXMethodDecl";
        private const string CxxConstructorDeclKind = "CXXConstructorDecl";
        private const string CxxDestructorDeclKind = "CXXDestructorDecl";
        private const string FunctionTemplateDeclKind = "FunctionTemplateDecl";
        private const string TemplateTypeParmDeclKind = "TemplateTypeParmDecl";
        private const string TemplateArgumentKind = "TemplateArgument";
        private const string ClassTemplateDeclKind = "ClassTemplateDecl";
        private const string ClassTemplateSpecializationDeclKind = "ClassTemplateSpecializationDecl";
        private const string AccessSpecDeclKind = "AccessSpecDecl";
        private const string ParmVarDeclKind = "ParmVarDecl";
        private const string FunctionDeclKind = "FunctionDecl";
        private const string EnumConstantDeclKind = "EnumConstantDecl";
        private const string EnumDeclKind = "EnumDecl";
        private const string FieldDeclKind = "FieldDecl";
        private const string VarDeclKind = "VarDecl";
        private const string TypeAliasDeclKind = "TypeAliasDecl";
        private const string StringLiteralKind = "StringLiteral";
        private const string IntegerLiteralKind = "IntegerLiteral";
        private const string AccessModifierPrivate = "private";
        private const string AccessModifierProtected = "protected";
        private const string AccessModifierPublic = "public";
        private const string RootNamespace = "Azure";
        private const string DetailsNamespacePostfix = "::_detail";
        private const string ImplicitConstrucorHintError = "Implicit constructor is found. Constructors must be explicitly declared.";
        private const string NonAccessModifierMemberError = "Found field without access modifier. Access modifier must be explicitly declared.";

        private static Regex _typeTokenizer = new Regex("[\\w:<>]+|[^\\w]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex _packageNameParser = new Regex("([A-Za-z_]*)", RegexOptions.Compiled);

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
            "uint8_t",
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

        private static HashSet<string> parentTypes = new HashSet<string>()
        {
            AccessModifierPrivate,
            AccessModifierProtected,
            AccessModifierPublic
        };

        public override string Name { get; } = "C++";

        public override string [] Extensions { get; } = { ".cppast" };

        public override bool CanUpdate(string versionString) => versionString != CurrentVersion;

        public override async Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis)
        {
            MemoryStream astStream = new MemoryStream();
            await stream.CopyToAsync(astStream);
            astStream.Position = 0;

            //Generate pacakge name from original file name
            string packageNamespace = "";
            var packageNameMatch = _packageNameParser.Match(originalName);
            if (packageNameMatch.Success)
            {
                packageNamespace = packageNameMatch.Groups[1].Value.Replace("_", "::");
            }

            CodeFileTokensBuilder builder = new CodeFileTokensBuilder();
            List<NavigationItem> navigation = new List<NavigationItem>();
            List<CodeDiagnostic> diagnostics = new List<CodeDiagnostic>();
            var archive = new ZipArchive(astStream);
            var astParser = new CppAstConverter();
            foreach (var entry in archive.Entries)
            {
                var root = new CppAstNode();
                astParser.ParseToAstTree(entry, root);
                BuildNodes(builder, navigation, diagnostics, root, packageNamespace);
            }

            return new CodeFile()
            {
                Name = originalName,
                Language = "C++",
                PackageName = packageNamespace,
                Tokens = builder.Tokens.ToArray(),
                Navigation = navigation.ToArray(),
                VersionString = CurrentVersion,
                Diagnostics = diagnostics.ToArray()
            };
        }

        private static void BuildNodes(CodeFileTokensBuilder builder, List<NavigationItem> navigation, List<CodeDiagnostic> diagnostic, CppAstNode root, string packageNamespace)
        {
            //Mapping of each namespace to it's leaf namespace nodes
            //These leaf nodes are processed to generate and group them together
            //C++ ast has declarations under same namespace in multiple files so these needs to be grouped for better presentation
            var namespaceLeafMap = new Dictionary<string, List<CppAstNode>>();
            var types = new HashSet<string>();

            var namespaceNodes = root.inner.Where(n => n.kind == NamespaceDeclKind && n.name == RootNamespace);
            bool foundFilterNamespace = false;
            foreach (var node in namespaceNodes)
            {
                var namespacebldr = new StringBuilder();
                var leafNamespaceNode = node;
                var currentNode = node;
                //Iterate until leaf namespace node and generate full namespace
                while (currentNode?.kind == NamespaceDeclKind)
                {
                    if (namespacebldr.Length > 0)
                    {
                        namespacebldr.Append("::");
                    }
                    namespacebldr.Append(currentNode.name);
                    leafNamespaceNode = currentNode;
                    currentNode = currentNode.inner?.FirstOrDefault(n => n.kind == NamespaceDeclKind);
                    if (leafNamespaceNode.inner?.Any(n => n.kind != NamespaceDeclKind) == true)
                    {
                        var nameSpace = namespacebldr.ToString();
                        if (!foundFilterNamespace && nameSpace.StartsWith(packageNamespace))
                        {
                            foundFilterNamespace = true;
                        }

                        if (!namespaceLeafMap.ContainsKey(nameSpace))
                        {
                            namespaceLeafMap[nameSpace] = new List<CppAstNode>();
                        }
                        namespaceLeafMap[nameSpace].Add(leafNamespaceNode);
                    }
                }
            }

            foreach (var nameSpace in namespaceLeafMap.Keys)
            {
                // Filter namespace based on file name if any of the namespace matches file name pattern
                // If no namespace matches file name then allow all namespaces to be part of review to avoid mandating file name convention
                if ((!foundFilterNamespace || nameSpace.StartsWith(packageNamespace)) && !nameSpace.EndsWith(DetailsNamespacePostfix))
                {
                    ProcessNamespaceNode(nameSpace);
                    builder.NewLine();
                    builder.NewLine();
                }
            }

            void ProcessNamespaceNode(string nameSpace)
            {
                NavigationItem currentNamespace = new NavigationItem()
                {
                    NavigationId = nameSpace,
                    Text = nameSpace,
                    Tags = { { "TypeKind", "namespace" } }
                };
                List<NavigationItem> currentNamespaceMembers = new List<NavigationItem>();

                builder.Keyword("namespace");
                builder.Space();
                var namespaceTokens = nameSpace.Split("::");
                foreach (var token in namespaceTokens)
                {
                    builder.Text(token);
                    builder.Space();
                    builder.Punctuation("{");
                    builder.Space();
                }
                builder.NewLine();
                //Process all nodes in namespace
                foreach (var leafNamespaceNode in namespaceLeafMap[nameSpace])
                {
                    if (leafNamespaceNode.inner != null)
                    {
                        foreach (var member in leafNamespaceNode.inner)
                        {
                            // Name space has mix of details namespace and classes
                            // API View should skip those sub details namespaces also
                            if (member.kind == NamespaceDeclKind)
                                continue;

                            builder.IncrementIndent();
                            ProcessNode(member, currentNamespaceMembers, nameSpace);
                            builder.DecrementIndent();
                        }
                    }
                }

                currentNamespace.ChildItems = currentNamespaceMembers.ToArray();
                navigation.Add(currentNamespace);
                builder.NewLine();
                for (int i = 0; i < namespaceTokens.Length; i++)
                    builder.Punctuation("}");
                builder.NewLine();
            }

            NavigationItem BuildDeclaration(string name, string kind, string parentId = "")
            {
                string definitionId = name;
                if (!string.IsNullOrEmpty(parentId))
                {
                    definitionId = parentId + "::" + name;
                }

                builder.Append(new CodeFileToken()
                {
                    DefinitionId = definitionId,
                    Kind = CodeFileTokenKind.TypeName,
                    Value = name,
                });
                types.Add(name);
                return new NavigationItem()
                {
                    NavigationId = definitionId,
                    Text = name,
                    Tags = { { "TypeKind", kind } }
                };
            }

            void BuildMemberDeclaration(string containerName, string name, string id = "")
            {
                if (string.IsNullOrEmpty(id))
                {
                    id = name;
                }
                builder.Append(new CodeFileToken()
                {
                    DefinitionId = containerName + "." + id,
                    Kind = CodeFileTokenKind.MemberName,
                    Value = name,
                });
            }

            string GenerateUniqueMethodId(CppAstNode methodNode)
            {
                var bldr = new StringBuilder();
                bldr.Append(methodNode.name);

                if (methodNode.inner != null)
                {
                    foreach (var parameterNode in methodNode.inner)
                    {
                        if (parameterNode.kind == ParmVarDeclKind)
                        {
                            bldr.Append(":");
                            bldr.Append(parameterNode.type.Replace(" ", "_"));
                        }
                    }
                }

                bldr.Append("::");
                var type = string.IsNullOrEmpty(methodNode.type) ? "void" : methodNode.type;
                bldr.Append(type.Replace(" ", "_"));
                return bldr.ToString();
            }

            NavigationItem ProcessClassNode(CppAstNode node, string parentName, List<CppAstNode> templateParams = null)
            {
                NavigationItem navigationItem = null;

                builder.Keyword(node.tagUsed);
                builder.Space();
                string nodeName = node.name;
                if (templateParams != null)
                {
                    nodeName += "<";
                    bool first = true;
                    foreach (var paramNode in templateParams)
                    {
                        if (!first)
                        {
                            nodeName += ", ";
                        }
                        nodeName += paramNode.name;
                    }
                    nodeName += ">";
                }
                if (!string.IsNullOrEmpty(nodeName))
                {
                    navigationItem = BuildDeclaration(nodeName, node.tagUsed, parentName);
                    builder.Space();
                }

                var memberNavigations = new List<NavigationItem>();
                var parents = node.inner?.Where(n => parentTypes.Contains(n.kind));
                if (parents != null)
                {
                    bool first = true;
                    //Show inheritance details
                    foreach (var parent in parents)
                    {
                        if (first)
                        {
                            builder.Punctuation(":");
                            builder.Space();
                            first = false;
                        }
                        else
                        {
                            builder.Punctuation(",");
                            builder.Space();
                        }
                        builder.Keyword(parent.access);
                        builder.Space();
                        if (parent.name != null)
                        {
                            BuildType(builder, parent.name, types);
                        }
                    }
                    builder.Space();
                }

                builder.NewLine();
                builder.WriteIndent();
                builder.Punctuation("{");
                builder.NewLine();
                //Double indentation for members since access modifier is not parent for members
                builder.IncrementIndent();
                builder.IncrementIndent();
                bool hasFoundDefaultAccessMembers = false;
                var id = parentName + "::" + node.name;

                if (node.inner != null)
                {
                    bool isPrivateMember = false;
                    string currentAccessModifier = "";
                    foreach (var childNode in node.inner)
                    {
                        if (childNode.kind == CxxRecordDeclKind && childNode.name == node.name)
                            continue;

                        // add public or protected access specifier
                        if (childNode.kind == AccessSpecDeclKind)
                        {
                            //Skip all private members
                            isPrivateMember = (childNode.access == AccessModifierPrivate);
                            if (isPrivateMember)
                            {
                                continue;
                            }
                            currentAccessModifier = childNode.access;
                            builder.DecrementIndent();
                            builder.WriteIndent();
                            builder.Keyword(childNode.access);
                            builder.Punctuation(":");
                            builder.IncrementIndent();
                            builder.NewLine();
                        }
                        else if (!isPrivateMember && !parentTypes.Contains(childNode.kind))
                        {
                            if (string.IsNullOrEmpty(currentAccessModifier) && !hasFoundDefaultAccessMembers)
                            {
                                hasFoundDefaultAccessMembers = true;
                            }
                            ProcessNode(childNode, memberNavigations, id);
                        }
                    }
                }

                builder.DecrementIndent();
                builder.DecrementIndent();
                builder.WriteIndent();
                builder.Punctuation("}");
                builder.Punctuation(";");
                builder.NewLine();
                builder.NewLine();

                if (navigationItem != null)
                {
                    navigationItem.ChildItems = memberNavigations.ToArray();
                }

                if (node.tagUsed == "class")
                {
                    if (node.inner?.Any(n => n.isimplicit == true && n.kind == CxxConstructorDeclKind) == true)
                    {
                        diagnostic.Add(new CodeDiagnostic("", navigationItem.NavigationId, ImplicitConstrucorHintError, ""));
                    }

                    if (hasFoundDefaultAccessMembers)
                    {
                        diagnostic.Add(new CodeDiagnostic("", navigationItem.NavigationId, NonAccessModifierMemberError, ""));
                    }
                }

                return navigationItem;
            }

            NavigationItem ProcessEnumNode(CppAstNode node)
            {
                builder.Keyword("enum");
                builder.Space();
                var navigationItem = BuildDeclaration(node.name, "enum");
                builder.NewLine();
                builder.WriteIndent();
                builder.Punctuation("{");
                builder.NewLine();
                builder.IncrementIndent();

                if (node.inner != null)
                {
                    foreach (var parameterNode in node.inner)
                    {
                        if (parameterNode.kind == EnumConstantDeclKind)
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
                }

                builder.DecrementIndent();
                builder.WriteIndent();
                builder.Punctuation("};");
                builder.NewLine();
                builder.NewLine();
                return navigationItem;
            }

            void ProcessFunctionDeclNode(CppAstNode node, string parentName)
            {
                if (node.isimplicit == true)
                {
                    builder.Keyword("implicit");
                    builder.Space();
                }

                if (node.isvirtual == true)
                {
                    builder.Keyword("virtual");
                    builder.Space();
                }

                if (node.inline == true)
                {
                    builder.Keyword("inline");
                    builder.Space();
                }

                if (!string.IsNullOrEmpty(node.storageClass))
                {
                    builder.Keyword(node.storageClass);
                    builder.Space();
                }

                if (node.type != null)
                {
                    BuildType(builder, node.type, types);
                    builder.Space();
                }

                BuildMemberDeclaration(parentName, node.name, GenerateUniqueMethodId(node));
                builder.Punctuation("(");
                bool first = true;
                if (node.inner != null)
                {
                    bool isMultiLineArgs = node.inner.Count > 1;
                    builder.IncrementIndent();
                    foreach (var parameterNode in node.inner)
                    {
                        if (parameterNode.kind == ParmVarDeclKind)
                        {
                            if (first)
                            {
                                first = false;
                            }
                            else
                            {
                                builder.Punctuation(",");
                                builder.Space();
                            }

                            if (isMultiLineArgs)
                            {
                                builder.NewLine();
                                builder.WriteIndent();
                            }
                            BuildType(builder, parameterNode.type, types);

                            if (!string.IsNullOrEmpty(parameterNode.name))
                            {
                                builder.Space();
                                builder.Text(parameterNode.name);
                            }
                        }
                    }
                    builder.DecrementIndent();
                }

                builder.Punctuation(")");
                //Add any postfix keywords if signature has any.
                // Few expamples are 'const noexcept' 
                if (!string.IsNullOrEmpty(node.keywords))
                {
                    foreach (var key in node.keywords.Split())
                    {
                        builder.Space();
                        builder.Keyword(key);
                    }
                }

                // If method is tagged as pure, delete, or default then it should be marked as "=<0|default|delete>"
                if (node.ispure == true)
                {
                    builder.Space();
                    builder.Punctuation("=");
                    builder.Space();
                    builder.Text("0");
                }
                else if (node.isdefault == true)
                {
                    builder.Space();
                    builder.Punctuation("=");
                    builder.Space();
                    builder.Keyword("default");
                }
                else if (node.isdelete == true)
                {
                    builder.Space();
                    builder.Punctuation("=");
                    builder.Space();
                    builder.Keyword("delete");
                }

                builder.Punctuation(";");
                builder.NewLine();
            }

            NavigationItem ProcessTemplateClassDeclNode(CppAstNode node, string parentName)
            {
                NavigationItem returnValue = null;
                builder.Keyword("template");
                builder.Space();

                if (node.inner != null)
                {
                    bool first = true;
                    builder.Punctuation("<");
                    List<CppAstNode> templateParams = new List<CppAstNode>();
                    foreach (var childnode in node.inner.Where(n => n.kind == TemplateTypeParmDeclKind))
                    {
                        if (!first)
                        {
                            builder.Punctuation(",");
                            builder.Space();
                        }

                        templateParams.Add(childnode);
                        BuildType(builder, childnode.name, types);
                    }
                    builder.Punctuation(">");
                    builder.NewLine();
                    builder.WriteIndent();
                    foreach (var childnode in node.inner.Where(n => n.kind == CxxRecordDeclKind))
                    {
                        returnValue = ProcessClassNode(childnode, parentName, templateParams);
                    }
                }
                return returnValue;
            }

            NavigationItem ProcessTemplateClassSpecializationDeclNode(CppAstNode node, string parentName)
            {
                NavigationItem returnValue = null;
                builder.Keyword("template");
                builder.Space();

                if (node.inner != null)
                {
                    builder.Punctuation("<>");
                    List<CppAstNode> templateParams = new List<CppAstNode>();
                    foreach (var childnode in node.inner.Where(n => n.kind == TemplateArgumentKind))
                    {
                        templateParams.Add(childnode);
                    }
                    builder.NewLine();
                    builder.WriteIndent();
                    foreach (var childnode in node.inner.Where(n => n.kind == CxxRecordDeclKind))
                    {
                        returnValue = ProcessClassNode(childnode, parentName, templateParams);
                    }
                }
                return returnValue;
            }


            void ProcessTemplateFuncDeclNode(CppAstNode node, string parentName)
            {
                builder.Keyword("template");
                builder.Space();

                if (node.inner != null)
                {
                    bool first = true;
                    builder.Punctuation("<");
                    foreach (var childnode in node.inner.Where(n => n.kind == TemplateTypeParmDeclKind))
                    {
                        if (!first)
                        {
                            builder.Punctuation(",");
                            builder.Space();
                        }
                        BuildType(builder, childnode.name, types);
                    }
                    builder.Punctuation(">");

                    builder.Space();
                    var methodNode = node.inner.FirstOrDefault(node => node.kind == CxxMethodDeclKind);
                    if (methodNode != null)
                    {
                        ProcessFunctionDeclNode(methodNode, parentName);
                    }
                }
            }

            void ProcessTypeAlias(CppAstNode node)
            {
                builder.Keyword("using");
                builder.Space();
                builder.Text(node.name);
                builder.Space();
                builder.Punctuation("=");
                builder.Space();
                BuildType(builder, node.type, types);
                builder.Punctuation(";");
                builder.NewLine();
            }

            void ProcessVarDecNode(CppAstNode node, string parentName)
            {
                if (node.constexpr == true)
                {
                    builder.Keyword("constexpr");
                    builder.Space();
                }
                if (!string.IsNullOrEmpty(node.storageClass))
                {
                    builder.Keyword(node.storageClass);
                    builder.Space();
                }

                //Remove left most const from type if it is constexpr
                string type = node.type;
                if (node.constexpr == true && type.StartsWith("const"))
                {
                    var regex = new Regex(Regex.Escape("const"));
                    type = regex.Replace(type, "", 1).Trim();
                }

                BuildType(builder, type, types);
                builder.Space();
                BuildMemberDeclaration(parentName, node.name);
                if (node.inner?.FirstOrDefault() is CppAstNode
                                exprNode)
                {
                    builder.Space();
                    builder.Punctuation("=");
                    builder.Space();
                    BuildExpression(builder, exprNode);
                }
                builder.Punctuation(";");
                builder.NewLine();
            }

            void ProcessNode(CppAstNode node, List<NavigationItem> navigationItems, string parentName)
            {
                NavigationItem currentNavItem = null;
                builder.WriteIndent();
                switch (node.kind)
                {
                    case CxxRecordDeclKind:
                        {
                            currentNavItem = ProcessClassNode(node, parentName);
                            builder.NewLine();
                            break;
                        }

                    case CxxConstructorDeclKind:
                    case CxxDestructorDeclKind:
                    case FunctionDeclKind:
                    case CxxMethodDeclKind:
                        {
                            ProcessFunctionDeclNode(node, parentName);
                            break;
                        }
                    case EnumDeclKind:
                        {
                            currentNavItem = ProcessEnumNode(node);
                            builder.NewLine();
                            break;
                        }

                    case FieldDeclKind:
                    case VarDeclKind:
                        {
                            ProcessVarDecNode(node, parentName);
                            break;
                        }

                    case TypeAliasDeclKind:
                        {
                            ProcessTypeAlias(node);
                            break;
                        }

                    case FunctionTemplateDeclKind:
                        {
                            ProcessTemplateFuncDeclNode(node, parentName);
                            break;
                        }
                    case ClassTemplateDeclKind:
                        {
                            currentNavItem = ProcessTemplateClassDeclNode(node, parentName);
                            break;
                        }
                    case ClassTemplateSpecializationDeclKind:
                        {
                            currentNavItem = ProcessTemplateClassSpecializationDeclNode(node, parentName);
                            break;
                        }
                    default:
                        builder.Text(node.ToString());
                        builder.NewLine();
                        break;
                }

                if (currentNavItem != null && navigationItems != null)
                {
                    navigationItems.Add(currentNavItem);
                }
            }

            void BuildType(CodeFileTokensBuilder builder, string type, HashSet<string> types)
            {
                foreach (Match typePartMatch in _typeTokenizer.Matches(type))
                {
                    var typePart = typePartMatch.ToString();
                    if (_keywords.Contains(typePart))
                    {
                        builder.Keyword(typePart);
                    }
                    else if (typePart.Contains("::"))
                    {
                        // Handle type usage before it's defintition
                        var typeNamespace = typePart.Substring(0, typePart.LastIndexOf("::"));
                        string typeValue = typePart;
                        string navigateToId = "";
                        if (types.Contains(typePart) || namespaceLeafMap.ContainsKey(typeNamespace))
                        {
                            typeValue = typePart.Substring(typePart.LastIndexOf("::") + 2);
                            navigateToId = typePart;
                        }
                        builder.Append(new CodeFileToken()
                        {
                            Kind = CodeFileTokenKind.TypeName,
                            NavigateToId = navigateToId,
                            Value = typeValue
                        });
                    }
                    else
                    {
                        builder.Text(typePart);
                    }
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
                case "StringLiteral":
                    builder.Append(exprNode.value, CodeFileTokenKind.StringLiteral);
                    break;
                default:
                    builder.Text(exprNode + " " + exprNode.value);
                    break;
            }
        }

        private class CppAstNode
        {
            public string id { get; set; }
            public string kind { get; set; }
            public string type { get; set; }
            public string name { get; set; }
            public string text { get; set; }
            public string value { get; set; }
            public List<CppAstNode> inner { get; set; }
            public string storageClass { get; set; }
            public bool? constexpr { get; set; }
            public bool? inline { get; set; }
            public bool? isimplicit { get; set; }
            public bool? isvirtual { get; set; }
            public bool? ispure { get; set; }
            public bool? isdefault { get; set; }
            public bool? isdelete { get; set; }
            public string tagUsed { get; set; }
            public string access { get; set; }
            public string keywords { get; set; }
            public override string ToString()
            {
                return $"{nameof(kind)}: {kind} {nameof(name)}: {name}";
            }
        }

        private class CppAstConverter
        {
            private static Regex _declKindParser = new Regex("\\w+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            private static Regex _methodOrParamParser = new Regex("([\\S]+)\\s'([^\\(']*)\\s?\\([^)]*\\)\\s?([^']*)'([\\w\\s]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            private static Regex _varOrParamParser = new Regex("([a-zA-Z0-9_]*)\\s'([^\\(']*)\\s?\\(?[^']*'", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            private static Regex _typeAliasParser = new Regex("([a-zA-Z]*)\\s'([^']*)'", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            private static Regex _classNameParser = new Regex("(struct|class|union)\\s?([\\w]*)(\\sdefinition)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            private static Regex _classTemplateNameParser = new Regex("\\s?([\\w_]*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            private static Regex _fieldDefParser = new Regex("([\\w]+)\\s'([^']+)'", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            private static Regex _accessType = new Regex("private|public|protected", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            private static Regex _stringLiteralParser = new Regex("\"([^\"]+)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            private static Regex _integerLiteralParser = new Regex("'int'\\s([0-9A-Fx]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            private static Regex _enumDeclParser = new Regex("(class)\\s([\\w]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            private static Regex _enumConstantDeclParser = new Regex("([\\w]+)+\\s'([\\w:]+)'", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            private static Regex _inheritanceParser = new Regex("(private|public|protected)\\s'([\\w:<>]+)'", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            private static Regex _templateArgumentParser = new Regex("'([\\w:_<>]+)':?'?([\\w_<>]+)?'?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            private static HashSet<string> _declarationKinds = new HashSet<string>()
            {
                NamespaceDeclKind,
                VarDeclKind,
                StringLiteralKind,
                IntegerLiteralKind,
                FunctionDeclKind,
                ParmVarDeclKind,
                CxxRecordDeclKind,
                CxxMethodDeclKind,
                FieldDeclKind,
                CxxConstructorDeclKind,
                CxxDestructorDeclKind,
                AccessSpecDeclKind,
                EnumDeclKind,
                EnumConstantDeclKind,
                TypeAliasDeclKind,
                FunctionTemplateDeclKind,
                ClassTemplateDeclKind,
                ClassTemplateSpecializationDeclKind,
                TemplateTypeParmDeclKind,
                TemplateArgumentKind,
                AccessModifierPublic,
                AccessModifierProtected,
                AccessModifierPrivate
            };

            private static HashSet<string> _skipOnlyCurrentNodeKinds = new HashSet<string>()
            {
                "ImplicitCastExpr",
                "TranslationUnitDecl"
            };

            private static string ParseNodeKind(string line)
            {
                var declTypeMatch = _declKindParser.Matches(line).FirstOrDefault();
                string nodeKind = declTypeMatch != null ? declTypeMatch.ToString() : null;
                return nodeKind;
            }

            private static bool ShouldProcessLine(string line, CppAstNode node, CppAstNode lastNode)
            {
                node.isimplicit = line.Split().Any(s => s.Equals("implicit"));
                //Skip Subtree if current node is root namespace and if that's not valid root name('Azure')
                bool isValidNamespace = true;
                if (node.kind == NamespaceDeclKind)
                {
                    // Check if last name was namespace
                    // If last node in stack is not namespace node then current namespace node is root node and it has to be valid name
                    isValidNamespace = lastNode.kind == NamespaceDeclKind || line.Contains(RootNamespace);
                }

                // Process line if it is within valid namespace and tokenKind is in valid list
                // Implicit line should be processed only if it is for ConstructorDecl or if the previous node was a TemplateArgument.
                return isValidNamespace &&
                    _declarationKinds.Contains(node.kind) &&
                    (node.isimplicit == false ||
                     ((node.kind == CxxConstructorDeclKind) ||
                     (lastNode.kind == ClassTemplateSpecializationDeclKind))
                    );
            }

            public void ParseToAstTree(ZipArchiveEntry zipEntry, CppAstNode astRoot)
            {
                StreamReader reader = new StreamReader(zipEntry.Open());
                //Use a stack to track tree level
                var patternStack = new Stack<string>();
                var astnodeStack = new Stack<CppAstNode>();
                astnodeStack.Push(astRoot);

                string line = reader.ReadLine();
                while (line != null)
                {
                    CppAstNode node = new CppAstNode();
                    node.kind = ParseNodeKind(line);
                    var prefix = line.Substring(0, line.IndexOf(node.kind));

                    //Prefix string in ast-dump is compared to identify tree depth
                    //If prefix stack is empty or last element is same type as new prefix then node is at same depth
                    //If new prefix is larger than last element then this new node is sub node
                    //If new prefix is smaller than last element in stack then this new node is at higher level.(Traverse all the way to find a matching level in stack)
                    //Stack always keep track of items equivalent to max depth so it will be less items in stack)
                    if (patternStack.Count > 0)
                    {
                        if (patternStack.Peek().Length == prefix.Length)
                        {
                            patternStack.Pop();
                            astnodeStack.Pop();
                        }
                        else if (patternStack.Peek().Length > prefix.Length)
                        {
                            while (patternStack.Count > 0 && patternStack.Peek().Length >= prefix.Length)
                            {
                                patternStack.Pop();
                                astnodeStack.Pop();
                            }
                        }
                    }
                    if (ShouldProcessLine(line, node, astnodeStack.Peek()))
                    {
                        ParseLine(line, ref node);

                        var parentNode = astnodeStack.Count > 0 ? astnodeStack.Pop() : astRoot;
                        if (parentNode.inner == null)
                            parentNode.inner = new List<CppAstNode>();

                        parentNode.inner.Add(node);
                        patternStack.Push(prefix);
                        astnodeStack.Push(parentNode);
                        astnodeStack.Push(node);
                    }
                    else if (!_skipOnlyCurrentNodeKinds.Contains(node.kind))
                    {
                        //skip anychild nodes of excluded node
                        line = reader.ReadLine();
                        while (line != null)
                        {
                            var newLinePrefix = line.Substring(0, line.IndexOf(ParseNodeKind(line)));
                            //Should not skip new line if it is at parent level or sibling
                            if (newLinePrefix.Length <= prefix.Length)
                                break;
                            line = reader.ReadLine();
                        }
                        continue;
                    }

                    line = reader.ReadLine();
                }
            }

            private static void ParseLine(string line, ref CppAstNode node)
            {
                string[] tokens = line.Split();
                //Set any common properties
                node.storageClass = tokens.Any(token => token == "static") ? "static" : "";
                node.constexpr = tokens.Any(token => token == "constexpr");
                node.inline = tokens.Any(token => token == "inline");
                node.isimplicit = tokens.Any(token => token == "implicit");

                switch (node.kind)
                {
                    case NamespaceDeclKind:
                    case FunctionTemplateDeclKind:
                    case TemplateTypeParmDeclKind:
                        node.name = tokens.LastOrDefault();
                        break;

                    case VarDeclKind:
                    case ParmVarDeclKind:
                        ParseFieldDecl(line, ref node, ref _varOrParamParser);
                        break;

                    case TypeAliasDeclKind:
                        ParseFieldDecl(line, ref node, ref _typeAliasParser);
                        break;

                    case CxxMethodDeclKind:
                    case CxxConstructorDeclKind:
                    case CxxDestructorDeclKind:
                    case FunctionDeclKind:
                        ParseMethodDecl(line, ref node, ref _methodOrParamParser);
                        break;

                    case CxxRecordDeclKind:
                        ParseClassDecl(line, ref node);
                        break;

                    case ClassTemplateSpecializationDeclKind:
                        ParseClassTemplateSpecializationDecl(line, ref node);
                        break;
                    case TemplateArgumentKind:
                        ParseTemplateArgument(line, ref node);
                        break;
                    case ClassTemplateDeclKind:
                        ParseClassTemplateDecl(line, ref node);
                        break;

                    case FieldDeclKind:
                        ParseFieldDecl(line, ref node, ref _fieldDefParser);
                        break;

                    case StringLiteralKind:
                    case IntegerLiteralKind:
                        ParseLiteralDecl(line, ref node);
                        break;

                    case AccessSpecDeclKind:
                        ParseAccessType(line, ref node);
                        break;

                    case EnumDeclKind:
                        ParseEnumDecl(line, ref node);
                        break;

                    case EnumConstantDeclKind:
                        ParseEnumConstDecl(line, ref node);
                        break;

                    case AccessModifierPublic:
                    case AccessModifierProtected:
                    case AccessModifierPrivate:
                        //Inheritence declaration has inheritance access level as token kinds
                        ParseInheritanceDecl(line, node);
                        break;

                    default:
                        node.name = "Not implemented";
                        node.type = "";
                        break;
                }
            }

            private static void ParseFieldDecl(string line, ref CppAstNode node, ref Regex regexPattern)
            {
                var match = regexPattern.Match(line);
                if (match.Success)
                {
                    node.name = match.Groups[1].Value;
                    node.type = match.Groups[2].Value.Trim();
                }
            }

            private static void ParseMethodDecl(string line, ref CppAstNode node, ref Regex regexPattern)
            {
                var match = regexPattern.Match(line);
                if (match.Success)
                {
                    node.name = match.Groups[1].Value;
                    if (node.kind != CxxConstructorDeclKind && node.kind != CxxDestructorDeclKind)
                    {
                        node.type = match.Groups[2].Value.Trim();
                    }
                    node.keywords = match.Groups[3].Value;
                    var qualifiers = match.Groups[4].Value.Split();
                    node.isvirtual = qualifiers.Any(q => q == "virtual");
                    node.ispure = qualifiers.Any(q => q == "pure");
                    node.isdefault = qualifiers.Any(q => q == "default");
                    node.isdelete = qualifiers.Any(q => q == "delete");
                }
            }

            private static void ParseClassDecl(string line, ref CppAstNode node)
            {
                var match = _classNameParser.Match(line);
                if (match.Success)
                {
                    node.tagUsed = match.Groups[1].Value;
                    node.name = match.Groups[2].Value;
                }
            }

            private static void ParseClassTemplateDecl(string line, ref CppAstNode node)
            {
                var match = _classTemplateNameParser.Match(line);
                if (match.Success)
                {
                    node.name = match.Groups[1].Value;
                }
            }

            private static void ParseClassTemplateSpecializationDecl(string line, ref CppAstNode node)
            {
                var match = _classNameParser.Match(line);
                if (match.Success)
                {
                    node.name = match.Groups[2].Value;
                }

            }
            private static void ParseTemplateArgument(string line, ref CppAstNode node)
            {
                var match = _templateArgumentParser.Match(line);
                if (match.Success)
                {
                    node.name = match.Groups[1].Value;
                    node.type = match.Groups[2].Value;
                }

            }

            private static void ParseInheritanceDecl(string line, CppAstNode node)
            {
                var match = _inheritanceParser.Match(line);
                if (match.Success)
                {
                    node.access = match.Groups[1].Value;
                    node.name = match.Groups[2].Value;
                }
            }

            private static void ParseAccessType(string line, ref CppAstNode node)
            {
                var match = _accessType.Match(line);
                if (match.Success)
                {
                    node.access = match.Value;
                }
            }

            private static void ParseLiteralDecl(string line, ref CppAstNode node)
            {
                Regex regex = _stringLiteralParser;
                if (node.kind == IntegerLiteralKind)
                {
                    regex = _integerLiteralParser;
                }

                var match = regex.Match(line);
                if (match.Success)
                {
                    node.value = match.Groups[1].Value;
                }
            }

            private static void ParseEnumDecl(string line, ref CppAstNode node)
            {
                var match = _enumDeclParser.Match(line);
                if (match.Success)
                {
                    node.tagUsed = match.Groups[1].Value;
                    node.name = match.Groups[2].Value;
                }
            }

            private static void ParseEnumConstDecl(string line, ref CppAstNode node)
            {
                var match = _enumConstantDeclParser.Match(line);
                if (match.Success)
                {
                    node.name = match.Groups[1].Value;
                }
            }
        }
    }
}
