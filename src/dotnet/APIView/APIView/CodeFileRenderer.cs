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
            bool isDeprecatedLine = false;

            foreach (var token in node)
            {
                switch(token.Kind)
                {
                    // One assumption here is that documention line will not have actual code in same line
                    // Assumption is based on that requirement to hide documention on demand
                    case CodeFileTokenKind.Newline:
                    case CodeFileTokenKind.DocumentRangeEnd:
                        list.Add(new CodeLine(stringBuilder.ToString(), currentId, isDocumentation));
                        currentId = null;
                        stringBuilder.Clear();
                        if (token.Kind == CodeFileTokenKind.DocumentRangeEnd)
                            isDocumentation = false;
                        break;

                    case CodeFileTokenKind.DocumentRangeStart:
                        isDocumentation = true;
                        break;

                    case CodeFileTokenKind.DeprecatedRangeStart:
                        isDeprecatedLine = true;
                        break;

                    case CodeFileTokenKind.DeprecatedRangeEnd:
                        isDeprecatedLine = false;
                        break;

                    default:
                        if (token.DefinitionId != null)
                        {
                            currentId = token.DefinitionId;
                        }
                        RenderToken(token, stringBuilder, isDeprecatedLine);
                        break;
                }                
            }
        }

        protected virtual void RenderToken(CodeFileToken token, StringBuilder stringBuilder, bool isDeprecatedLine)
        {
            if (token.Value != null)
            {
                stringBuilder.Append(token.Value);
            }
        }
    }
}