using System.Text;

namespace APIView
{
    public class HTMLRendererAPIV : TreeRendererAPIV
    {
        protected override void RenderPunctuation(StringBuilder builder, string word)
        {
            builder.Append(word.Replace("<", "&lt;").Replace(">", "&gt;"));
        }

        protected override void RenderClass(StringBuilder builder, string word)
        {
            builder.Append("<span class=\"class\">").Append(word).Append("</span>");
        }

        protected override void RenderKeyword(StringBuilder builder, string word)
        {
            builder.Append("<span class=\"keyword\">").Append(word).Append("</span>");
        }

        protected override void RenderName(StringBuilder builder, string word)
        {
            builder.Append("<span class=\"name\">").Append(word).Append("</span>");
        }

        protected override void RenderNewline(StringBuilder builder)
        {
            builder.Append("<br />");
        }

        protected override void RenderSpecialName(StringBuilder builder, string word)
        {
            builder.Append("<span class=\"specialName\">").Append(word).Append("</span>");
        }

        protected override void RenderType(StringBuilder builder, string word)
        {
            builder.Append("<span class=\"type\">").Append(word).Append("</span>");
        }

        protected override void RenderValue(StringBuilder builder, string word)
        {
            builder.Append("<span class=\"value\">").Append(word).Append("</span>");
        }
    }
}
