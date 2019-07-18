using System.Linq;
using System.Text;

namespace APIView
{
    public class HTMLRendererAPIV : TreeRendererAPIV
    {
        protected override void RenderClassDefinition(StringBuilder builder, NamedTypeAPIV nt)
        {
            builder.Append("<a href=\"#\" id=\"").Append(EscapeHTML(nt.NavigationID)).Append("\" class=\"class commentable\">").
                Append(EscapeHTML(nt.Name)).Append("</a>");
        }

        protected override void RenderEnumDefinition(StringBuilder builder, NamedTypeAPIV nt)
        {
            builder.Append("<a href=\"#\" id=\"").Append(nt.NavigationID).Append("\" class=\"enum commentable\">").
                Append(EscapeHTML(nt.Name)).Append("</a>");
        }

        protected override void RenderPunctuation(StringBuilder builder, string word)
        {
            builder.Append(EscapeHTML(word));
        }

        protected override void RenderEnum(StringBuilder builder, TokenAPIV t)
        {
            builder.Append("<a href=\"#").Append(t.NavigationID).Append("\" class=\"enum\">")
                .Append(EscapeHTML(t.DisplayString)).Append("</a>");
        }

        protected override void RenderClass(StringBuilder builder, TokenAPIV t)
        {
            builder.Append("<a href=\"#").Append(t.NavigationID).Append("\" class=\"class\">")
                .Append(EscapeHTML(t.DisplayString)).Append("</a>");
        }

        protected override void RenderConstructor(StringBuilder builder, MethodAPIV m)
        {
            builder.Append("<a href=\"#").Append(EscapeHTML(m.ClassNavigationID)).Append("\" class=\"class\">")
                .Append(EscapeHTML(m.Name)).Append("</a>");
        }

        protected override void RenderKeyword(StringBuilder builder, string word)
        {
            builder.Append("<span class=\"keyword\">").Append(EscapeHTML(word)).Append("</span>");
        }

        protected override void RenderMethod(StringBuilder builder, MethodAPIV m)
        {
            builder.Append("<a id=\"").Append(m.Id).Append("\" class=\"name commentable\">").
                Append(m.Name).Append("</a>");
        }

        protected override void RenderName(StringBuilder builder, string word)
        {
            builder.Append("<span class=\"name\">").Append(EscapeHTML(word)).Append("</span>");
        }

        protected override void RenderNamespace(StringBuilder builder, NamespaceAPIV ns)
        {
            builder.Append("<span id=\"").Append(ns.NavigationID).Append("\" class=\"name\">")
                .Append(EscapeHTML(ns.Name)).Append("</span>");
        }

        protected override void RenderNewline(StringBuilder builder)
        {
            builder.Append("<br />");
        }

        protected override void RenderSpecialName(StringBuilder builder, string word)
        {
            builder.Append("<span class=\"specialName\">").Append(EscapeHTML(word)).Append("</span>");
        }

        protected override void RenderToken(StringBuilder builder, TokenAPIV t)
        {
            switch (t.Type)
            {
                case TypeReferenceAPIV.TokenType.BuiltInType:
                    RenderKeyword(builder, t.DisplayString);
                    break;
                case TypeReferenceAPIV.TokenType.ClassType:
                    RenderClass(builder, t);
                    break;
                case TypeReferenceAPIV.TokenType.EnumType:
                    RenderEnum(builder, t);
                    break;
                case TypeReferenceAPIV.TokenType.TypeArgument:
                    RenderType(builder, t.DisplayString);
                    break;
                case TypeReferenceAPIV.TokenType.ValueType:
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
