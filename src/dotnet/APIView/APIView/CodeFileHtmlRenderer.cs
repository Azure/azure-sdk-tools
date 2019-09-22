// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text;
using APIView;

namespace ApiView
{
    public class CodeFileHtmlRenderer: CodeFileRenderer
    {
        protected override void RenderToken(CodeFileToken token, StringBuilder stringBuilder)
        {
            if (token.Value == null)
            {
                return;
            }

            string elementClass = "";
            string id = token.DefinitionId;

            switch (token.Kind)
            {
                case  CodeFileTokenKind.TypeName:
                    elementClass = "class";
                    break;
                case CodeFileTokenKind.MemberName:
                    elementClass = "name";
                    break;
                case CodeFileTokenKind.Keyword:
                    elementClass = "keyword";
                    break;
                case CodeFileTokenKind.StringLiteral:
                    elementClass = "value";
                    break;
            }

            string href = null;

            if (token.DefinitionId != null)
            {
                elementClass += " commentable";
                href = "#";
            }

            if (token.NavigateToId != null)
            {
                href = "#" + token.NavigateToId;
            }

            if (!string.IsNullOrEmpty(elementClass))
            {
                stringBuilder.Append("<");
                var a = !string.IsNullOrEmpty(href);

                if (a)
                {
                    stringBuilder.Append("a");
                    stringBuilder.Append(" href=\"").Append(href).Append("\"");
                }
                else
                {
                    stringBuilder.Append("span");
                }
                if (!string.IsNullOrEmpty(id))
                {
                    stringBuilder.Append(" id=\"").Append(id).Append("\"");
                }
                stringBuilder.Append(" class=\"").Append(elementClass).Append("\"");
                stringBuilder.Append(">");
                stringBuilder.Append(token.Value);

                if (a)
                {
                    stringBuilder.Append("</a>");
                }
                else
                {
                    stringBuilder.Append("</span>");
                }
            }
            else
            {
                stringBuilder.Append(EscapeHTML(token.Value));
            }
        }

        private string EscapeHTML(string word)
        {
            return word.Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}