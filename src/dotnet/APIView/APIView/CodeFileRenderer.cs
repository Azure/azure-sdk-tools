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

        public CodeLine[] Render(CodeFile file)
        {
            var list = new List<CodeLine>();
            Render(list, file.Tokens);
            return list.ToArray();
        }

        private void Render(List<CodeLine> list, IEnumerable<CodeFileToken> node)
        {
            var stringBuilder = new StringBuilder();
            string currentId = null;
            bool isDocumentation = false;
            bool isDeprecatedToken = false;
            bool isDocumentationLine = false;

            foreach (var token in node)
            {
                switch(token.Kind)
                {
                    case CodeFileTokenKind.Newline:
                        //Close documentation span if within doc range
                        if (isDocumentation)
                        {
                            CloseDocumentationRange(stringBuilder);
                        }
                        list.Add(new CodeLine(stringBuilder.ToString(), currentId, isDocumentationLine));
                        currentId = null;
                        stringBuilder.Clear();
                        //Start documentation span if tokens still in documentation range
                        if (isDocumentation)
                        {
                            StartDocumentationRange(stringBuilder);
                        }
                        //Reset flag for line documentation. This will be set to false if atleast one token is not a doc
                        isDocumentationLine = isDocumentation;
                        break;

                    case CodeFileTokenKind.DocumentRangeStart:
                        isDocumentation = true;
                        isDocumentationLine = (stringBuilder.Length == 0);
                        StartDocumentationRange(stringBuilder);
                        break;

                    case CodeFileTokenKind.DocumentRangeEnd:
                        isDocumentation = false;
                        CloseDocumentationRange(stringBuilder);
                        break;

                    case CodeFileTokenKind.DeprecatedRangeStart:
                        isDeprecatedToken = true;
                        break;

                    case CodeFileTokenKind.DeprecatedRangeEnd:
                        isDeprecatedToken = false;
                        break;

                    default:
                        if (token.DefinitionId != null)
                        {
                            currentId = token.DefinitionId;
                        }
                        RenderToken(token, stringBuilder, isDeprecatedToken);
                        if (!isDocumentation)
                        {
                            isDocumentationLine = false;
                        }
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