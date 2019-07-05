using System.Text;

namespace APIView
{
    public class HTMLRendererAPIV : TreeRendererAPIV
    {
        protected override void RenderImplementations(NamedTypeAPIV nt, StringBuilder builder)
        {
            builder.Append(": ");
            foreach (var i in nt.Implementations)
            {
                RenderClass(builder, i.Replace("<", "&lt;").Replace(">", "&gt;"));
                builder.Append(", ");
            }
            builder.Length -= 2;
            builder.Append(" ");
        }

        protected override void RenderTypeParameters(NamedTypeAPIV nt, StringBuilder builder)
        {
            builder.Length -= 1;
            builder.Append("&lt;");
            foreach (TypeParameterAPIV tp in nt.TypeParameters)
            {
                Render(tp, builder);
                builder.Append(", ");
            }
            builder.Length -= 2;
            builder.Append("&gt; ");
        }

        protected override void RenderClass(StringBuilder builder, string word)
        {
            builder.Append("<font class=\"class\">").Append(word).Append("</font>");
        }

        protected override void RenderKeyword(StringBuilder builder, string word)
        {
            builder.Append("<font class=\"keyword\">").Append(word).Append("</font>");
        }

        protected override void RenderName(StringBuilder builder, string word)
        {
            builder.Append("<font class=\"name\">").Append(word).Append("</font>");
        }

        protected override void RenderNewline(StringBuilder builder)
        {
            builder.Append("<br />");
        }

        protected override void RenderSpecialName(StringBuilder builder, string word)
        {
            builder.Append("<font class=\"specialName\">").Append(word).Append("</font>");
        }

        protected override void RenderType(StringBuilder builder, string word)
        {
            builder.Append("<font class=\"type\">").Append(word).Append("</font>");
        }

        protected override void RenderValue(StringBuilder builder, string word)
        {
            builder.Append("<font class=\"value\">").Append(word).Append("</font>");
        }
    }
}
