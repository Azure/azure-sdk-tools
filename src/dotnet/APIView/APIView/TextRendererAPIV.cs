using System.Text;

namespace APIView
{
    public class TextRendererAPIV : TreeRendererAPIV
    {
        protected override void RenderPunctuation(StringBuilder builder, string word)
        {
            builder.Append(word.Replace("&lt;", "<").Replace("&gt;", ">"));
        }

        protected override void RenderClass(StringBuilder builder, string word)
        {
            builder.Append(word);
        }

        protected override void RenderKeyword(StringBuilder builder, string word)
        {
            builder.Append(word);
        }

        protected override void RenderName(StringBuilder builder, string word)
        {
            builder.Append(word);
        }

        protected override void RenderNewline(StringBuilder builder)
        {
            builder.AppendLine();
        }

        protected override void RenderSpecialName(StringBuilder builder, string word)
        {
            builder.Append(word);
        }

        protected override void RenderType(StringBuilder builder, string word)
        {
            builder.Append(word);
        }

        protected override void RenderValue(StringBuilder builder, string word)
        {
            builder.Append(word);
        }
    }
}
