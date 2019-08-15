using System.Text;

namespace ApiView
{
    public class TextRendererApiView : TreeRendererApiView
    {
        protected override void RenderClassDefinition(StringBuilder builder, NamedTypeApiView nt)
        {
            builder.Append(nt.Name);
        }

        protected override void RenderEnumDefinition(StringBuilder builder, NamedTypeApiView nt)
        {
            builder.Append(nt.Name);
        }

        protected override void RenderPunctuation(StringBuilder builder, string word)
        {
            builder.Append(word.Replace("&lt;", "<").Replace("&gt;", ">"));
        }

        protected override void RenderEnum(StringBuilder builder, TokenApiView t)
        {
            builder.Append(t.DisplayString);
        }

        protected override void RenderClass(StringBuilder builder, TokenApiView t)
        {
            builder.Append(t.DisplayString);
        }

        protected override void RenderCommentable(StringBuilder builder, string id, string name)
        {
            builder.Append(name);
        }

        protected override void RenderConstructor(StringBuilder builder, MethodApiView m)
        {
            builder.Append(m.Name);
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

        protected override void RenderToken(StringBuilder builder, TokenApiView t)
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
