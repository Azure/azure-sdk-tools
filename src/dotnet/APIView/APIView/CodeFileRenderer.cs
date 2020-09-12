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
        const string DOCUMENTATION_SPAN_START = "<span class=\"documentation\">";
        const string DOCUMENTATION_SPAN_END = "</span>";
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
            //This will be set to true by default when a new line starts and 
            // set to false when any non documentation token is found within the line
            bool isLineAllDocumentation = false;

            foreach (var token in node)
            {
                switch(token.Kind)
                {
                    case CodeFileTokenKind.Newline:
                        //Close documentation span if within doc range
                        if(isDocumentation)
                        {
                            stringBuilder.Append(DOCUMENTATION_SPAN_END);
                        }
                        list.Add(new CodeLine(stringBuilder.ToString(), currentId, isLineAllDocumentation));
                        currentId = null;
                        stringBuilder.Clear();
                        //Start documentation span if tokens still in documentation range
                        if(isDocumentation)
                        {
                            stringBuilder.Append(DOCUMENTATION_SPAN_START);
                        }
                        //Reset flag for line documentation. This will be set to false if atleast one token is not a doc
                        isLineAllDocumentation = true;
                        break;

                    case CodeFileTokenKind.DocumentRangeStart:
                        isDocumentation = true;
                        stringBuilder.Append(DOCUMENTATION_SPAN_START);
                        break;

                    case CodeFileTokenKind.DocumentRangeEnd:
                        isDocumentation = false;
                        stringBuilder.Append(DOCUMENTATION_SPAN_END);
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
                        if(!isDocumentation)
                        {
                            isLineAllDocumentation = false;
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
    }
}