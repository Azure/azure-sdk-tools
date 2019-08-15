using System.Text;

namespace ApiView
{
    public class HTMLRendererApiView : TreeRendererApiView
    {
        protected override void RenderClassDefinition(StringBuilder builder, NamedTypeApiView nt)
        {
            builder.Append("<a href=\"#\" id=\"").Append(EscapeHTML(nt.Id)).Append("\" class=\"class commentable\">").
                Append(EscapeHTML(nt.Name)).Append("</a>");
        }

        protected override void RenderEnumDefinition(StringBuilder builder, NamedTypeApiView nt)
        {
            builder.Append("<a href=\"#\" id=\"").Append(nt.Id).Append("\" class=\"enum commentable\">").
                Append(EscapeHTML(nt.Name)).Append("</a>");
        }

        protected override void RenderPunctuation(StringBuilder builder, string word)
        {
            builder.Append(EscapeHTML(word));
        }

        protected override void RenderEnum(StringBuilder builder, TokenApiView t)
        {
            builder.Append("<a href=\"#").Append(t.Id).Append("\" class=\"enum\">")
                .Append(EscapeHTML(t.DisplayString)).Append("</a>");
        }

        protected override void RenderClass(StringBuilder builder, TokenApiView t)
        {
            builder.Append("<a href=\"#").Append(t.Id).Append("\" class=\"class\">")
                .Append(EscapeHTML(t.DisplayString)).Append("</a>");
        }

        protected override void RenderCommentable(StringBuilder builder, string id, string name)
        {
            builder.Append("<a id=\"").Append(id).Append("\" class=\"name commentable\">").
                Append(name).Append("</a>");
        }

        protected override void RenderConstructor(StringBuilder builder, MethodApiView m)
        {
            builder.Append("<a href=\"#\" id=\"").Append(EscapeHTML(m.Id)).Append("\" class=\"class commentable\">")
                .Append(EscapeHTML(m.Name)).Append("</a>");
        }

        protected override void RenderKeyword(StringBuilder builder, string word)
        {
            builder.Append("<span class=\"keyword\">").Append(EscapeHTML(word)).Append("</span>");
        }

        protected override void RenderName(StringBuilder builder, string word)
        {
            builder.Append("<span class=\"name\">").Append(EscapeHTML(word)).Append("</span>");
        }

        protected override void RenderNewline(StringBuilder builder)
        {
            builder.Append("<br />");
        }

        protected override void RenderSpecialName(StringBuilder builder, string word)
        {
            builder.Append("<span class=\"specialName\">").Append(EscapeHTML(word)).Append("</span>");
        }

        protected override void RenderToken(StringBuilder builder, TokenApiView t)
        {
            switch (t.Type)
            {
                case TypeReferenceApiView.TokenType.BuiltInType:
                    RenderKeyword(builder, t.DisplayString);
                    break;
                case TypeReferenceApiView.TokenType.ClassType:
                    RenderClass(builder, t);
                    break;
                case TypeReferenceApiView.TokenType.EnumType:
                    RenderEnum(builder, t);
                    break;
                case TypeReferenceApiView.TokenType.TypeArgument:
                    RenderType(builder, t.DisplayString);
                    break;
                case TypeReferenceApiView.TokenType.ValueType:
                    RenderValue(builder, t.DisplayString);
                    break;
                default:
                    RenderPunctuation(builder, t.DisplayString);
                    break;
            }
        }

        protected override void RenderType(StringBuilder builder, string word)
        {
            var shortName = word.Substring(word.LastIndexOf(".") + 1);
            builder.Append("<a href=\"#").Append(EscapeHTML(shortName)).Append("\" class=\"type\">")
                .Append(EscapeHTML(word)).Append("</a>");
        }

        protected override void RenderValue(StringBuilder builder, string word)
        {
            builder.Append("<span class=\"value\">").Append(EscapeHTML(word)).Append("</span>");
        }

        private string EscapeHTML(string word)
        {
            return word.Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}
