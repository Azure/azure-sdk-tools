// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIView;
using System;
using System.Collections.Generic;
using System.Text;

namespace ApiView
{
    public class CodeFileRenderer
    {
        public static CodeFileRenderer Instance = new CodeFileRenderer();

        public CodeLine[] Render(CodeFile file, bool showDocumentation = false, bool enableSkipDiff = false)
        {
            var list = new List<CodeLine>();
            Render(list, file.Tokens, showDocumentation, enableSkipDiff);
            return list.ToArray();
        }

        private void Render(List<CodeLine> list, IEnumerable<CodeFileToken> node, bool showDocumentation, bool enableSkipDiff)
        {
            var stringBuilder = new StringBuilder();
            string currentId = null;
            bool isDocumentationRange = false;
            bool isDeprecatedToken = false;
            bool isSkipDiffRange = false;
            Stack<(string, string)> nodesInProcess = new Stack<(string, string)>();
            string lastHeadingEncountered = null;

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
                        // Set parent and content class if current line is infoldable panel
                        string lineClass = "";
                        if (nodesInProcess.Count > 0)
                        {
                            var nodesInProcessAsArray = nodesInProcess.ToArray();
                            for (int i = 0; i < nodesInProcessAsArray.Length; i++)
                            {
                                if (i == 0 && nodesInProcessAsArray[i].Item2.Equals("heading"))
                                {
                                    lineClass += (nodesInProcessAsArray[i].Item1 + "-heading ");
                                }
                                else if (i > 0 && nodesInProcessAsArray[i].Item2.Equals("heading"))
                                {
                                    break;
                                }
                                else
                                {
                                    lineClass += (nodesInProcessAsArray[i].Item1 + "-content ");
                                }
                            }
                            lineClass = lineClass.Trim();
                        }
                        list.Add(new CodeLine(stringBuilder.ToString(), currentId, lineClass));
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
                        nodesInProcess.Push((token.Value, "heading"));
                        lastHeadingEncountered = token.Value;
                        RenderToken(token, stringBuilder, isDeprecatedToken);
                        break;

                    case CodeFileTokenKind.FoldableSectionContentStart:
                        nodesInProcess.Push((lastHeadingEncountered, "content"));
                        break;

                    case CodeFileTokenKind.FoldableSectionContentEnd:
                        nodesInProcess.Pop();
                        if (nodesInProcess.Peek().Item2.Equals("heading"))
                        {
                            nodesInProcess.Pop();
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
}
