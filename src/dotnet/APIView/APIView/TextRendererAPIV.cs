using System.Text;

namespace APIView
{
    public class TextRendererAPIV : TreeRendererAPIV
    {
        protected override void RenderImplementations(NamedTypeAPIV nt, StringBuilder builder)
        {
            builder.Append(": ");
            foreach (var i in nt.Implementations)
            {
                RenderClass(builder, i);
                builder.Append(", ");
            }
            builder.Length -= 2;
            builder.Append(" ");
        }

        protected override void RenderTypeParameters(NamedTypeAPIV nt, StringBuilder builder)
        {
            builder.Length -= 1;
            builder.Append("<");
            foreach (TypeParameterAPIV tp in nt.TypeParameters)
            {
                Render(tp, builder);
                builder.Append(", ");
            }
            builder.Length -= 2;
            builder.Append("> ");
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
