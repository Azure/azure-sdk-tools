// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIView;
using APIView.Model;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ApiView
{
    public class CodeFileRenderer
    {
        public static CodeFileRenderer Instance = new CodeFileRenderer();

        public RenderResult Render(CodeFile file, bool showDocumentation = false, bool enableSkipDiff = false)
        {
            var codeLines = new List<CodeLine>();
            var sections = new List<TreeNode<CodeLine>>();
            Render(codeLines, file.Tokens, showDocumentation, enableSkipDiff, sections);
            return new RenderResult(codeLines.ToArray(), sections);
        }

        private void Render(List<CodeLine> list, IEnumerable<CodeFileToken> node, bool showDocumentation, bool enableSkipDiff, List<TreeNode<CodeLine>> sections)
        {
            var stringBuilder = new StringBuilder();
            string currentId = null;
            bool isDocumentationRange = false;
            bool isDeprecatedToken = false;
            bool isSkipDiffRange = false;
            Stack<NodeInProcess> nodesInProcess = new Stack<NodeInProcess>();
            HashSet<string> lineIds = new HashSet<string>(); // Used to ensure there are no duplicate IDs
            int indentSize = 0;
            int lineNumber = 0;
            TreeNode<CodeLine> section = null;

            foreach (var token in node)
            {
                // Skip all tokens till range end
                if (enableSkipDiff && isSkipDiffRange && token.Kind != CodeFileTokenKind.SkipDiffRangeEnd)
                    continue;

                if (!showDocumentation && isDocumentationRange && token.Kind != CodeFileTokenKind.DocumentRangeEnd)
                    continue;

                switch(token.Kind)
                {
                    case CodeFileTokenKind.Newline:
                        var lineId = SanitizeLineId(currentId, lineIds);
                        CodeLine codeLine = new CodeLine(stringBuilder.ToString(), lineId, String.Empty, ++lineNumber, indentSize);
                        if (nodesInProcess.Count > 0)
                        {
                            var nodesInProcessAsArray = nodesInProcess.ToArray();
                            for (int i = 0; i < nodesInProcessAsArray.Length; i++)
                            {
                                if (i == 0 && nodesInProcessAsArray[i].SectionType.Equals("heading"))
                                {
                                    if (nodesInProcessAsArray[i].SectionNode == null)
                                    {
                                        section = new TreeNode<CodeLine>(codeLine);
                                        nodesInProcessAsArray[i].SectionNode = section;
                                        list.Add(codeLine);
                                    }
                                    else
                                    {
                                        section = nodesInProcessAsArray[i].SectionNode.AddChild(codeLine);
                                    }
                                    
                                }
                                else if (i > 0 && nodesInProcessAsArray[i].SectionType.Equals("heading"))
                                {
                                    section = nodesInProcessAsArray[i].SectionNode?.Parent;
                                    break;
                                }
                                else
                                {
                                    nodesInProcessAsArray[i].SectionNode.AddChild(codeLine);
                                }
                            }
                        }
                        else 
                        {
                            list.Add(codeLine);
                        }
                        currentId = null;
                        stringBuilder.Clear();
                        break;

                    case CodeFileTokenKind.DocumentRangeStart:
                        StartDocumentationRange(stringBuilder);
                        isDocumentationRange = true;
                        break;

                    case CodeFileTokenKind.DocumentRangeEnd:
                        CloseDocumentationRange(stringBuilder);
                        isDocumentationRange = false;
                        break;

                    case CodeFileTokenKind.DeprecatedRangeStart:
                        isDeprecatedToken = true;
                        break;

                    case CodeFileTokenKind.DeprecatedRangeEnd:
                        isDeprecatedToken = false;
                        break;

                    case CodeFileTokenKind.SkipDiffRangeStart:
                        isSkipDiffRange = true;
                        break;

                    case CodeFileTokenKind.SkipDiffRangeEnd:
                        isSkipDiffRange = false;
                        break;

                    case CodeFileTokenKind.FoldableSectionHeading:
                        nodesInProcess.Push(new NodeInProcess(section, "heading"));
                        RenderToken(token, stringBuilder, isDeprecatedToken);
                        break;

                    case CodeFileTokenKind.FoldableSectionContentStart:
                        nodesInProcess.Push(new NodeInProcess(section, "content"));
                        indentSize++;
                        break;

                    case CodeFileTokenKind.FoldableSectionContentEnd:
                        nodesInProcess.Pop();
                        if (nodesInProcess.Peek().SectionType.Equals("heading"))
                        {
                            if (nodesInProcess.Count == 1)
                            {
                                sections.Add(nodesInProcess.Peek().SectionNode);
                            }
                            nodesInProcess.Pop();
                        }
                        indentSize--;
                        break;
                    default:
                        if (token.DefinitionId != null)
                        {
                            currentId = token.DefinitionId;
                        }
                        RenderToken(token, stringBuilder, isDeprecatedToken);
                        break;
                }                
            }
        }

        private string SanitizeLineClass(string lineClass)
        {
            if (!String.IsNullOrEmpty(lineClass))
            {
                var result = lineClass.ToLower();
                result = Regex.Replace(result, "[^a-z_0-9-]", "");
                result = Regex.Replace(result, "^[0-9]+", "");
                result = Regex.Replace(result, "//s+", "");
                return result;
            }
            return lineClass;
        }

        private string SanitizeLineId(string lineId, HashSet<string> lineIds)
        {
            int resultAsInt;
            if (Int32.TryParse(lineId, out resultAsInt))
            {
                return resultAsInt.ToString();
            }

            // Ensure the id is valid html id
            if (!String.IsNullOrWhiteSpace(lineId))
            {
                var result = lineId.ToLower();
                result = Regex.Replace(result, "[^a-z_0-9-:.]", "");
                result = Regex.Replace(result, "^[0-9]+", "");
                result = Regex.Replace(result, "//s+", "");

                // Remove duplicates by appending or incrementing a number as suffix of string
                if (lineIds.Contains(result))
                {
                    do
                    {
                        var suffixCount = Regex.Match(result, "[0-9]+$").Value;
                        if (!String.IsNullOrWhiteSpace(suffixCount))
                        {
                            int suffixCountAsInt = Int32.Parse(suffixCount);
                            result = Regex.Replace(result, $"{suffixCount}$", $"{++suffixCountAsInt}");
                        }
                        else
                        {
                            result += $"_1";
                        }
                    }
                    while (lineIds.Contains(result));
                }
                lineIds.Add(result);
                return result;
            }
            return lineId;
        }

        protected virtual void RenderToken(CodeFileToken token, StringBuilder stringBuilder, bool isDeprecatedToken)
        {
            if (token.Value != null)
            {
                stringBuilder.Append(token.Value);
            }
        }

        // Below two methods are HTML renderer specific and implemented in htmlrender class
        // These methods should not render anything for text renderer so keeping it empty
        protected virtual void StartDocumentationRange(StringBuilder stringBuilder) { }
        protected virtual void CloseDocumentationRange(StringBuilder stringBuilder) { }
    }

    public struct NodeInProcess
    {
        public NodeInProcess(TreeNode<CodeLine> sectionNode, string sectionType)
        {
            SectionNode = sectionNode;
            SectionType = sectionType;
        }

        public TreeNode<CodeLine> SectionNode { get; set; }
        public string SectionType { get; }
    }

    public struct RenderResult
    {
        public RenderResult(CodeLine[] codeLines, List<TreeNode<CodeLine>> sections)
        {
            CodeLines = codeLines;
            Sections = sections;
        }

        public CodeLine[] CodeLines { get; }
        public List<TreeNode<CodeLine>> Sections { get; }
    }
}
