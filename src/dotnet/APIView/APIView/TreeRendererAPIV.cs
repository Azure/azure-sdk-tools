using System.Linq;
using System.Text;

namespace ApiView
{
    public abstract class TreeRendererApiv
    {
        private void AppendIndents(StringBuilder builder, int indents)
        {
            for (int i = 0; i < indents; i++)
            {
                builder.Append("    ");
            }
        }

        public StringListApiv Render(AssemblyApiv assembly)
        {
            var list = new StringListApiv();
            Render(assembly.GlobalNamespace, list);
            return list;
        }

        public void Render(AttributeApiv a, StringListApiv list, int indents = 0)
        {
            var builder = new StringBuilder();
            AppendIndents(builder, indents);
            builder.Append("[");
            Render(a.Type, builder);

            if (a.ConstructorArgs.Any())
            {
                builder.Append("(");
                foreach (var arg in a.ConstructorArgs)
                {
                    if (arg.IsNamed)
                    {
                        builder.Append(arg.Name);
                        builder.Append(" = ");
                    }
                    RenderValue(builder, arg.Value);
                    builder.Append(", ");
                }
                builder.Length -= 2;
                builder.Append(")");
            }

            builder.Append("]");
            list.Add(new LineApiv(builder.ToString(), a.Id));
        }

        public void Render(EventApiv e, StringListApiv list, int indents = 0)
        {
            var builder = new StringBuilder();
            AppendIndents(builder, indents);
            RenderKeyword(builder, e.Accessibility);
            builder.Append(" ");
            RenderKeyword(builder, "event");
            builder.Append(" ");
            Render(e.Type, builder);
            builder.Append(" ");
            RenderCommentable(builder, e.Id, e.Name);
            builder.Append(";");
            list.Add(new LineApiv(builder.ToString(), e.Id));
        }

        public void Render(FieldApiv f, StringListApiv list, int indents = 0)
        {
            var builder = new StringBuilder();
            AppendIndents(builder, indents);
            RenderKeyword(builder, f.Accessibility);
            builder.Append(" ");

            if (f.IsStatic)
            {
                RenderKeyword(builder, "static");
                builder.Append(" ");
            }
            if (f.IsReadOnly)
            {
                RenderKeyword(builder, "readonly");
                builder.Append(" ");
            }
            if (f.IsVolatile)
            {
                RenderKeyword(builder, "volatile");
                builder.Append(" ");
            }
            if (f.IsConstant)
            {
                RenderKeyword(builder, "const");
                builder.Append(" ");
            }

            Render(f.Type, builder);
            builder.Append(" ");
            RenderCommentable(builder, f.Id, f.Name);

            if (f.IsConstant)
            {
                if (f.Type.IsString)
                {
                    builder.Append(" = ");
                    RenderValue(builder, "\"" + f.Value + "\"");
                }
                else
                    builder.Append(" = ").Append(f.Value);
            }

            builder.Append(";");
            list.Add(new LineApiv(builder.ToString(), f.Id));
        }

        public void Render(MethodApiv m, StringListApiv list, int indents = 0)
        {
            if (m.Attributes.Any())
            {
                foreach (var attribute in m.Attributes)
                {
                    Render(attribute, list, indents);
                }
            }

            var builder = new StringBuilder();
            AppendIndents(builder, indents);

            if (!m.IsInterfaceMethod)
            {
                RenderKeyword(builder, m.Accessibility);
                builder.Append(" ");
            }

            if (m.IsStatic)
            {
                RenderKeyword(builder, "static");
                builder.Append(" ");
            }
            if (m.IsVirtual)
            {
                RenderKeyword(builder, "virtual");
                builder.Append(" ");
            }
            if (m.IsSealed)
            {
                RenderKeyword(builder, "sealed");
                builder.Append(" ");
            }
            if (m.IsOverride)
            {
                RenderKeyword(builder, "override");
                builder.Append(" ");
            }
            if (m.IsAbstract && !m.IsInterfaceMethod)
            {
                RenderKeyword(builder, "abstract");
                builder.Append(" ");
            }
            if (m.IsExtern)
            {
                RenderKeyword(builder, "extern");
                builder.Append(" ");
            }

            if (m.ReturnType != null)
            {
                Render(m.ReturnType, builder);
                builder.Append(" ");
            }

            if (m.IsConstructor)
                RenderConstructor(builder, m);
            else
                RenderCommentable(builder, m.Id, m.Name);

            if (m.TypeParameters.Any())
            {
                RenderPunctuation(builder, "<");
                foreach (var tp in m.TypeParameters)
                {
                    Render(tp, builder);
                    builder.Append(", ");
                }
                builder.Length -= 2;
                RenderPunctuation(builder, ">");
            }

            builder.Append("(");
            if (m.Parameters.Any())
            {
                foreach (ParameterApiv p in m.Parameters)
                {
                    Render(p, builder);
                    builder.Append(", ");
                }
                builder.Length -= 2;
            }

            if (m.IsInterfaceMethod || m.IsAbstract)
                builder.Append(");");
            else
                builder.Append(") { }");
            list.Add(new LineApiv(builder.ToString(), m.Id));
        }

        public void Render(NamedTypeApiv nt, StringListApiv list, int indents = 0)
        {
            var builder = new StringBuilder();
            AppendIndents(builder, indents);
            if (nt.IsSealed)
            {
                RenderKeyword(builder, "sealed");
                builder.Append(" ");
            }
            RenderKeyword(builder, nt.Accessibility);
            builder.Append(" ");
            if (nt.IsStatic) {
                RenderKeyword(builder, "static");
                builder.Append(" ");
            }
            RenderKeyword(builder, nt.TypeKind);
            builder.Append(" ");

            indents++;

            switch (nt.TypeKind)
            {
                case ("enum"):
                    RenderEnumDefinition(builder, nt);
                    builder.Append(" ");

                    if (nt.EnumUnderlyingType.Tokens[0].DisplayString != "int")
                    {
                        builder.Append(": ");
                        Render(nt.EnumUnderlyingType, builder);
                        builder.Append(" ");
                    }
                    builder.Append("{");
                    list.Add(new LineApiv(builder.ToString(), nt.Id));

                    foreach (FieldApiv f in nt.Fields)
                    {
                        builder = new StringBuilder();
                        AppendIndents(builder, indents);
                        builder.Append(f.Name).Append(" = ");
                        RenderValue(builder, f.Value.ToString());
                        builder.Append(",");
                        list.Add(new LineApiv(builder.ToString()));
                    }

                    builder = new StringBuilder();
                    AppendIndents(builder, indents - 1);
                    builder.Append("}");
                    list.Add(new LineApiv(builder.ToString()));
                    break;

                case ("delegate"):
                    foreach (MethodApiv m in nt.Methods)
                    {
                        if (m.Name.Equals("Invoke"))
                        {
                            Render(m.ReturnType, builder);
                            builder.Append(" ");
                            RenderName(builder, nt.Name);
                            builder.Append("(");

                            if (m.Parameters.Any())
                            {
                                foreach (ParameterApiv p in m.Parameters)
                                {
                                    Render(p, builder);
                                    builder.Append(", ");
                                }
                                builder.Length -= 2;
                            }
                        }
                    }
                    builder.Append(") { }");
                    list.Add(new LineApiv(builder.ToString(), nt.Id));
                    break;

                default:
                    RenderClassDefinition(builder, nt);
                    builder.Append(" ");

                    if (nt.TypeParameters.Any())
                    {
                        builder.Length -= 1;
                        RenderPunctuation(builder, "<");
                        foreach (var tp in nt.TypeParameters)
                        {
                            Render(tp, builder);
                            builder.Append(", ");
                        }
                        builder.Length -= 2;
                        RenderPunctuation(builder, ">");
                        builder.Append(" ");
                    }

                    // add any implemented types to string
                    if (nt.Implementations.Any())
                    {
                        builder.Append(": ");
                        foreach (var i in nt.Implementations)
                        {
                            Render(i, builder);
                            builder.Append(", ");
                        }
                        builder.Length -= 2;
                        builder.Append(" ");
                    }
                    builder.Append("{");
                    list.Add(new LineApiv(builder.ToString(), nt.Id));

                    // add any types declared in this type's body
                    foreach (FieldApiv f in nt.Fields)
                    {
                        Render(f, list, indents);
                    }
                    foreach (PropertyApiv p in nt.Properties)
                    {
                        Render(p, list, indents);
                    }
                    foreach (EventApiv e in nt.Events)
                    {
                        Render(e, list, indents);
                    }
                    foreach (MethodApiv m in nt.Methods)
                    {
                        Render(m, list, indents);
                    }
                    foreach (NamedTypeApiv n in nt.NamedTypes)
                    {
                        Render(n, list, indents);
                    }

                    builder = new StringBuilder();
                    AppendIndents(builder, indents - 1);
                    builder.Append("}");
                    list.Add(new LineApiv(builder.ToString()));
                    break;
            }
        }

        public void Render(NamespaceApiv ns, StringListApiv list, int indents = 0)
        {
            var builder = new StringBuilder();
            var isGlobalNamespace = ns.Name == "<global namespace>";
            if (!isGlobalNamespace && ns.NamedTypes.Any())
            {
                AppendIndents(builder, indents);
                RenderKeyword(builder, "namespace");
                builder.Append(" ");
                RenderCommentable(builder, ns.Id, ns.Name);
                builder.Append(" {");
                list.Add(new LineApiv(builder.ToString(), ns.Id));
            }

            foreach (NamedTypeApiv nt in ns.NamedTypes)
            {
                Render(nt, list, indents + 1);
            }

            if (!isGlobalNamespace && ns.NamedTypes.Any())
            {
                builder = new StringBuilder();
                AppendIndents(builder, indents);
                builder.Append("}");
                list.Add(new LineApiv(builder.ToString()));
            }

            foreach (NamespaceApiv n in ns.Namespaces)
            {
                Render(n, list, indents);
            }
        }

        public void Render(ParameterApiv p, StringBuilder builder, int indents = 0)
        {
            if (p.Attributes.Any())
            {
                builder.Append("[");
                foreach (string attribute in p.Attributes)
                {
                    RenderName(builder, attribute);
                    builder.Append(", ");
                }
                builder.Length -= 2;
                builder.Append("] ");
                RenderNewline(builder);
                AppendIndents(builder, indents);
            }

            if (p.RefKind != Microsoft.CodeAnalysis.RefKind.None)
            {
                RenderKeyword(builder, p.RefKind.ToString().ToLower());
                builder.Append(" ");
            }
            Render(p.Type, builder);

            builder.Append(" ").Append(p.Name);
            if (p.HasExplicitDefaultValue)
            {
                if (p.Type.IsString)
                {
                    builder.Append(" = ");
                    if (p.ExplicitDefaultValue == null)
                        RenderSpecialName(builder, "null");
                    else
                        RenderValue(builder, "\"" + p.ExplicitDefaultValue.ToString() + "\"");
                }

                else
                {
                    builder.Append(" = ");
                    if (p.ExplicitDefaultValue == null)
                        RenderSpecialName(builder, "null");
                    else
                        RenderValue(builder, p.ExplicitDefaultValue.ToString());
                }
            }
        }

        public void Render(PropertyApiv p, StringListApiv list, int indents = 0)
        {
            var builder = new StringBuilder();
            AppendIndents(builder, indents);
            RenderKeyword(builder, p.Accessibility);
            builder.Append(" ");
            Render(p.Type, builder);
            builder.Append(" ");
            RenderCommentable(builder, p.Id, p.Name);
            builder.Append(" { ");
            RenderKeyword(builder, "get");
            builder.Append("; ");

            if (p.HasSetMethod)
            {
                RenderKeyword(builder, "set");
                builder.Append("; ");
            }

            builder.Append("}");
            list.Add(new LineApiv(builder.ToString(), p.Id));
        }

        public void Render(TypeParameterApiv tp, StringBuilder builder, int indents = 0)
        {
            if (tp.Attributes.Any())
            {
                builder.Append("[");
                foreach (string attribute in tp.Attributes)
                {
                    RenderName(builder, attribute);
                    builder.Append(", ");
                }
                builder.Length -= 2;
                builder.Append("] ");
                RenderNewline(builder);
                AppendIndents(builder, indents);
            }

            RenderType(builder, tp.Name);
        }

        public void Render(TypeReferenceApiv type, StringBuilder builder)
        {
            if (type == null || type?.Tokens == null)
                return;
            foreach (var token in type.Tokens)
            {
                RenderToken(builder, token);
            }
        }

        protected abstract void RenderClassDefinition(StringBuilder builder, NamedTypeApiv nt);

        protected abstract void RenderEnumDefinition(StringBuilder builder, NamedTypeApiv nt);

        protected abstract void RenderPunctuation(StringBuilder builder, string word);

        protected abstract void RenderEnum(StringBuilder builder, TokenApiv t);

        protected abstract void RenderClass(StringBuilder builder, TokenApiv t);

        protected abstract void RenderCommentable(StringBuilder builder, string id, string name);

        protected abstract void RenderConstructor(StringBuilder builder, MethodApiv m);

        protected abstract void RenderKeyword(StringBuilder builder, string word);

        protected abstract void RenderName(StringBuilder builder, string word);

        protected abstract void RenderNewline(StringBuilder builder);

        protected abstract void RenderSpecialName(StringBuilder builder, string word);

        protected abstract void RenderToken(StringBuilder builder, TokenApiv t);

        protected abstract void RenderType(StringBuilder builder, string word);

        protected abstract void RenderValue(StringBuilder builder, string word);
    }
}
