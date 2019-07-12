using System.Linq;
using System.Text;

namespace APIView
{
    public class HTMLRendererAPIV : TreeRendererAPIV
    {
        protected override void RenderClassDefinition(StringBuilder builder, NamedTypeAPIV nt)
        {
            builder.Append("<span id=\"").Append(EscapeHTML(nt.NavigationID)).Append("\" class=\"class\">")
                .Append(EscapeHTML(nt.Name)).Append("</span>");
        }

        protected override void RenderEnumDefinition(StringBuilder builder, NamedTypeAPIV nt)
        {
            builder.Append("<span id=\"").Append(nt.NavigationID).Append("\" class=\"enum\">")
                .Append(EscapeHTML(nt.Name)).Append("</span>");
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
