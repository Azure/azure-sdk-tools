using System.Linq;
using System.Text;

namespace APIView
{
    public class HTMLRendererAPIV : TreeRendererAPIV
    {
        protected override void RenderClassDefinition(StringBuilder builder, NamedTypeAPIV nt)
        {
            builder.Append("<span id=\"").Append(nt.NavigationID.Replace("<", "&lt;").Replace(">", "&gt;")).Append("\" class=\"class\">")
                .Append(nt.Name.Replace("<", "&lt;").Replace(">", "&gt;")).Append("</span>");
        }

        protected override void RenderEnumDefinition(StringBuilder builder, NamedTypeAPIV nt)
        {
            builder.Append("<span id=\"").Append(nt.NavigationID).Append("\" class=\"enum\">")
                .Append(nt.Name.Replace("<", "&lt;").Replace(">", "&gt;")).Append("</span>");
        }

        protected override void RenderPunctuation(StringBuilder builder, string word)
        {
            builder.Append(word.Replace("<", "&lt;").Replace(">", "&gt;"));
        }

        protected override void RenderEnum(StringBuilder builder, Token t)
        {
            builder.Append("<a href=\"#").Append(t.NavigationID).Append("\" class=\"enum\">")
                .Append(t.DisplayString.Replace("<", "&lt;").Replace(">", "&gt;")).Append("</a>");
        }

        protected override void RenderClass(StringBuilder builder, Token t)
        {
            builder.Append("<a href=\"#").Append(t.NavigationID).Append("\" class=\"class\">")
                .Append(t.DisplayString.Replace("<", "&lt;").Replace(">", "&gt;")).Append("</a>");
        }

        protected override void RenderConstructor(StringBuilder builder, MethodAPIV m)
        {
            builder.Append("<a href=\"#").Append(m.ClassNavigationID.Replace("<", "&lt;").Replace(">", "&gt;")).Append("\" class=\"class\">")
                .Append(m.Name.Replace("<", "&lt;").Replace(">", "&gt;")).Append("</a>");
        }

        protected override void RenderKeyword(StringBuilder builder, string word)
        {
            builder.Append("<span class=\"keyword\">").Append(word.Replace("<", "&lt;").Replace(">", "&gt;")).Append("</span>");
        }

        protected override void RenderName(StringBuilder builder, string word)
        {
            builder.Append("<span class=\"name\">").Append(word.Replace("<", "&lt;").Replace(">", "&gt;")).Append("</span>");
        }

        protected override void RenderNamespace(StringBuilder builder, NamespaceAPIV ns)
        {
            builder.Append("<span id=\"").Append(ns.NavigationID).Append("\" class=\"name\">")
                .Append(ns.Name.Replace("<", "&lt;").Replace(">", "&gt;")).Append("</span>");
        }

        protected override void RenderNewline(StringBuilder builder)
        {
            builder.Append("<br />");
        }

        protected override void RenderSpecialName(StringBuilder builder, string word)
        {
            builder.Append("<span class=\"specialName\">").Append(word.Replace("<", "&lt;").Replace(">", "&gt;")).Append("</span>");
        }

        protected override void RenderType(StringBuilder builder, string word)
        {
            var shortName = word.Substring(word.LastIndexOf(".") + 1);
            builder.Append("<a href=\"#").Append(shortName.Replace("<", "&lt;").Replace(">", "&gt;")).Append("\" class=\"type\">")
                .Append(word.Replace("<", "&lt;").Replace(">", "&gt;")).Append("</a>");
        }

        protected override void RenderValue(StringBuilder builder, string word)
        {
            builder.Append("<span class=\"value\">").Append(word.Replace("<", "&lt;").Replace(">", "&gt;")).Append("</span>");
        }
    }
}
