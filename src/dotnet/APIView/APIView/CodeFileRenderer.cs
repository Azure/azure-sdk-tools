// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIView;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

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
            HashSet<string> lineIds = new HashSet<string>(); // Used to ensure there are no duplicate IDs

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
                        lineClass = SanitizeLineClass(lineClass);
                        var lineId = SanitizeLineId(currentId, lineIds);

                        list.Add(new CodeLine(stringBuilder.ToString(), lineId, lineClass));
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

        private string SanitizeLineClass(string lineClass)
        {
            if (!String.IsNullOrEmpty(lineClass))
            {
                var result = lineClass.ToLower();
                result = Regex.Replace(result, "[^a-z_0-9-\\s+]", "");
                result = Regex.Replace(result, "^[0-9]+", "");
                return result;
            }
            return lineClass;
        }

        private string SanitizeLineId(string lineId, HashSet<string> lineIds)
        {
            if (!String.IsNullOrEmpty(lineId))
            {
                var result = lineId.ToLower();
                result = Regex.Replace(result, "[^a-z_0-9-:.]", "");
                result = Regex.Replace(result, "^[0-9]+", "");

                if (lineIds.Contains(result))
                {
                    do
                    {
                        if (result.EndsWith("1"))
                        {
                            result += '1';
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
}
