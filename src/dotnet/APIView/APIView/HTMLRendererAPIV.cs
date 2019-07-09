using System.Text;

namespace APIView
{
    public class HTMLRendererAPIV : TreeRendererAPIV
    {
        protected override void RenderClassDefinition(StringBuilder builder, string word)
        {
            builder.Append("<span id=\"").Append(word.Replace("<", "&lt;").Replace(">", "&gt;")).Append("\" class=\"class\">")
                .Append(word.Replace("<", "&lt;").Replace(">", "&gt;")).Append("</span>");
        }

        protected override void RenderPunctuation(StringBuilder builder, string word)
        {
            builder.Append(word.Replace("<", "&lt;").Replace(">", "&gt;"));
        }

        protected override void RenderEnum(StringBuilder builder, string word)
        {
            builder.Append("<span id=\"").Append(word.Replace("<", "&lt;").Replace(">", "&gt;")).Append("\" class=\"enum\">")
                .Append(word.Replace("<", "&lt;").Replace(">", "&gt;")).Append("</span>");
        }

        protected override void RenderClass(StringBuilder builder, string word)
        {
            var typeParamIndex = word.LastIndexOf("<");
            string newWord;
            if (typeParamIndex >= 0)
                newWord = word.Remove(typeParamIndex);
            else
                newWord = word;
            var shortName = newWord.Substring(newWord.LastIndexOf(".") + 1);
            builder.Append("<a href=\"#").Append(shortName.Replace("<", "&lt;").Replace(">", "&gt;")).Append("\" class=\"class\">")
                .Append(word.Replace("<", "&lt;").Replace(">", "&gt;")).Append("</a>");
        }

        protected override void RenderKeyword(StringBuilder builder, string word)
        {
            builder.Append("<span class=\"keyword\">").Append(word.Replace("<", "&lt;").Replace(">", "&gt;")).Append("</span>");
        }

        protected override void RenderName(StringBuilder builder, string word)
        {
            builder.Append("<span class=\"name\">").Append(word.Replace("<", "&lt;").Replace(">", "&gt;")).Append("</span>");
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
