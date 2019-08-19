// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text;
using APIView;

namespace ApiView
{
    public class CodeFileRenderer
    {
        public StringListApiView Render(CodeFile file)
        {
            var list = new StringListApiView();
            Render(list, file.Tokens);
            return list;
        }

        private void Render(StringListApiView list, IEnumerable<CodeFileToken> node)
        {
            var stringBuilder = new StringBuilder();
            string currentId = null;
            foreach (var token in node)
            {
                if (token.Kind == CodeFileTokenKind.Newline)
                {
                    list.Add(new LineApiView(stringBuilder.ToString(), currentId));
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