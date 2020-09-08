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

            foreach (var token in node)
            {
                switch(token.Kind)
                {
                    case CodeFileTokenKind.Newline:
                        list.Add(new CodeLine(stringBuilder.ToString(), currentId));
                        currentId = null;
                        stringBuilder.Clear();
                        break;

                    case CodeFileTokenKind.DocumentRangeStart:
                        isDocumentation = true;
                        break;

                    case CodeFileTokenKind.DocumentRangeEnd:
                        isDocumentation = false;
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
                        RenderToken(token, stringBuilder, isDeprecatedToken, isDocumentation);
                        break;
                }                
            }
        }

        protected virtual void RenderToken(CodeFileToken token, StringBuilder stringBuilder, bool isDeprecatedToken, bool isDocumentation)
        {
            if (token.Value != null)
            {
                stringBuilder.Append(token.Value);
            }
        }
    }
}