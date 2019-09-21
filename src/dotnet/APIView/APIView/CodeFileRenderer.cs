// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using APIView;

namespace ApiView
{
    public class CodeFileRenderer
    {
        public StringListApiView Render(CodeFile file)
        {
            var list = new StringListApiView();
            Render(list, file.Tokens, file.Diagnostics ?? Array.Empty<CodeDiagnostic>());
            return list;
        }

        private void Render(StringListApiView list, IEnumerable<CodeFileToken> node, CodeDiagnostic[] fileDiagnostics)
        {
            var stringBuilder = new StringBuilder();
            string currentId = null;
            foreach (var token in node)
            {
                if (token.Kind == CodeFileTokenKind.Newline)
                {
                    list.Add(new LineApiView(stringBuilder.ToString(), currentId)
                    {
                        Diagnostics = fileDiagnostics.Where(d => d.TargetId == currentId).ToArray()
                    });
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