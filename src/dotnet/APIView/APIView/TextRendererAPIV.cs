using System.Text;

namespace APIView
{
    public class TextRendererAPIV : TreeRendererAPIV
    {
        protected override void RenderClassDefinition(StringBuilder builder, NamedTypeAPIV nt)
        {
            builder.Append(nt.Name);
        }

        protected override void RenderEnumDefinition(StringBuilder builder, NamedTypeAPIV nt)
        {
            builder.Append(nt.Name);
        }

        protected override void RenderPunctuation(StringBuilder builder, string word)
        {
            builder.Append(word.Replace("&lt;", "<").Replace("&gt;", ">"));
        }

        protected override void RenderEnum(StringBuilder builder, TokenAPIV t)
        {
            builder.Append(t.DisplayString);
        }

        protected override void RenderClass(StringBuilder builder, TokenAPIV t)
        {
            builder.Append(t.DisplayString);
        }

        protected override void RenderConstructor(StringBuilder builder, MethodAPIV m)
        {
            builder.Append(m.Name);
        }

        protected override void RenderKeyword(StringBuilder builder, string word)
        {
            builder.Append(word);
        }

        protected override void RenderMethod(StringBuilder builder, MethodAPIV m)
        {
            builder.Append(m.Name);
        }

        protected override void RenderName(StringBuilder builder, string word)
        {
            builder.Append(word);
        }

        protected override void RenderNamespace(StringBuilder builder, NamespaceAPIV ns)
        {
            builder.Append(ns.Name);
        }

        protected override void RenderNewline(StringBuilder builder)
        {
            builder.AppendLine();
        }

        protected override void RenderSpecialName(StringBuilder builder, string word)
        {
            builder.Append(word);
        }

        protected override void RenderToken(StringBuilder builder, TokenAPIV t)
        {
            builder.Append(t.DisplayString);
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
