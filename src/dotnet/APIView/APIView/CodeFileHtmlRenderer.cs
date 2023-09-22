// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIView;
using System.Text;
using System.Web;

namespace ApiView
{
    public class CodeFileHtmlRenderer : CodeFileRenderer
    {
        private const string DOCUMENTATION_SPAN_START = "<span class=\"documentation\">";
        private const string DOCUMENTATION_SPAN_END = "</span>";
        private readonly bool _readOnly;

        protected CodeFileHtmlRenderer(bool readOnly)
        {
            _readOnly = readOnly;
        }

        public static CodeFileHtmlRenderer Normal { get; } = new CodeFileHtmlRenderer(false);
        public static CodeFileHtmlRenderer ReadOnly { get; } = new CodeFileHtmlRenderer(true);

        protected override void RenderToken(CodeFileToken token, StringBuilder stringBuilder, bool isDeprecatedToken, bool isHiddenApiToken)
        {
            if (token.Value == null)
            {
                return;
            }

            string elementClass = "";
            string id = token.DefinitionId;

            switch (token.Kind)
            {
                case CodeFileTokenKind.TypeName:
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
                case CodeFileTokenKind.Comment:
                    elementClass = "code-comment";
                    break;
            }

            if (isDeprecatedToken)
            {
                elementClass += " deprecated";
            }

            if (isHiddenApiToken)
            {
                elementClass += " hidden-api";
            }

            string href = null;

            if (token.DefinitionId != null && !_readOnly)
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
                var a = !_readOnly && !string.IsNullOrEmpty(href);

                if (a)
                {
                    stringBuilder.Append("a");
                    stringBuilder.Append(" href=\"").Append(href).Append("\"");
                }
                else
                {
                    stringBuilder.Append("span");
                }
                if (!string.IsNullOrEmpty(id) && !_readOnly)
                {
                    stringBuilder.Append(" id=\"").Append(id).Append("\"");
                }
                stringBuilder.Append(" class=\"").Append(elementClass).Append("\"");
                stringBuilder.Append(">");
                stringBuilder.Append(HttpUtility.HtmlEncode(token.Value));

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

        protected override void StartDocumentationRange(StringBuilder stringBuilder)
        {
            stringBuilder.Append(DOCUMENTATION_SPAN_START);
        }
        protected override void CloseDocumentationRange(StringBuilder stringBuilder) 
        {
            stringBuilder.Append(DOCUMENTATION_SPAN_END);
        }

        private string EscapeHTML(string word)
        {
            return word.Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}