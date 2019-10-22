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
            foreach (var token in node)
            {
                if (token.Kind == CodeFileTokenKind.Newline)
                {
                    list.Add(new CodeLine(stringBuilder.ToString(), currentId));
                    currentId = null;
                    stringBuilder.Clear();
                }
                else
                {
                    if (token.DefinitionId != null)
                    {
                        currentId = token.DefinitionId;
                    }

                    RenderToken(token, stringBuilder);
                }
            }
        }

        protected virtual void RenderToken(CodeFileToken token, StringBuilder stringBuilder)
        {
            if (token.Value != null)
            {
                stringBuilder.Append(token.Value);
            }
        }
    }
}