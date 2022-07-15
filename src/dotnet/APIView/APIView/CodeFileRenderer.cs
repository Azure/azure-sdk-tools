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
            var sections = new Dictionary<int,TreeNode<CodeLine>>();
            Render(codeLines, file.Tokens, showDocumentation, enableSkipDiff, sections);
            return new RenderResult(codeLines.ToArray(), sections);
        }

        private void Render(List<CodeLine> list, IEnumerable<CodeFileToken> node, bool showDocumentation, bool enableSkipDiff, Dictionary<int,TreeNode<CodeLine>> sections)
        {
            var stringBuilder = new StringBuilder();
            string currentId = null;
            bool isDocumentationRange = false;
            bool isDeprecatedToken = false;
            bool isSkipDiffRange = false;
            Stack<SectionType> nodesInProcess = new Stack<SectionType>();
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
                        int ? sectionKey = (nodesInProcess.Count > 0 && section == null) ? sections.Count: null;
                        CodeLine codeLine = new CodeLine(stringBuilder.ToString(), currentId, String.Empty, ++lineNumber, sectionKey);
                        if (nodesInProcess.Count > 0)
                        {
                            if (nodesInProcess.Peek().Equals(SectionType.Heading))
                            {
                                if (section == null)
                                {
                                    section = new TreeNode<CodeLine>(codeLine);
                                    list.Add(codeLine);
                                }
                                else
                                {
                                    section = section.AddChild(codeLine);
                                }
                            }
                            else 
                            {
                                section.AddChild(codeLine);
                            }
                        }
                        else
                        {
                            if (section != null)
                            {
                                sections.Add(sections.Count, section);
                                section = null;
                            }
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
                        nodesInProcess.Push(SectionType.Heading);
                        RenderToken(token, stringBuilder, isDeprecatedToken);
                        break;

                    case CodeFileTokenKind.FoldableSectionContentStart:
                        nodesInProcess.Push(SectionType.Content);
                        break;

                    case CodeFileTokenKind.FoldableSectionContentEnd:
                        section = (section.IsRoot) ? section : section.Parent;
                        nodesInProcess.Pop();
                        if (nodesInProcess.Peek().Equals(SectionType.Heading))
                        {
                            nodesInProcess.Pop();
                        }
                        if (nodesInProcess.Count == 0 && section != null)
                        {
                            sections.Add(sections.Count, section);
                            section = null;
                        }
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

    public struct RenderResult
    {
        public RenderResult(CodeLine[] codeLines, Dictionary<int,TreeNode<CodeLine>> sections)
        {
            CodeLines = codeLines;
            Sections = sections;
        }

        public CodeLine[] CodeLines { get; }
        public Dictionary<int, TreeNode<CodeLine>> Sections { get; }
    }

    enum SectionType
    {
        Heading,
        Content
    }
}
