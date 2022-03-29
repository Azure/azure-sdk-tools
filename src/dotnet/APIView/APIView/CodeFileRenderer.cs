// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIView;
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
            // this will be used to track nested foldable panels if present
            var foldableParentStack = new Stack<string>();
            string currentId = null;
            bool isDocumentationRange = false;
            bool isDeprecatedToken = false;
            bool isSkipDiffRange = false;
            // IF isfoldableRange is set then child nodes will be named as content of parent node
            bool isFoldableRange = false;
            string nodeName = null;

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
                        if (nodeName != null)
                        {
                            lineClass = nodeName + (isFoldableRange ?"-content" : "-parent");
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

                    case CodeFileTokenKind.FoldableContentStart:
                        // In case of nested foldable panel, push current parent name to stack
                        if (nodeName != null)
                        {
                            foldableParentStack.Push(nodeName);
                        }
                        nodeName = token.Value;
                        isFoldableRange = true;
                        break;

                    case CodeFileTokenKind.FoldableContentEnd:
                        // Foldable content panel is completed.
                        // Pop previous parent or reset foldable range if no longer a foldable panel.
                        if (foldableParentStack.Count > 0)
                        {
                            nodeName = foldableParentStack.Pop();
                        }
                        else
                        {
                            isFoldableRange = false;
                            nodeName = null;
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
