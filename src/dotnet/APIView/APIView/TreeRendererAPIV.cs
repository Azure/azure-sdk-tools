using System.Linq;
using System.Text;

namespace APIView
{
    public abstract class TreeRendererAPIV
    {
        private void AppendIndents(StringBuilder builder, int indents)
        {
            for (int i = 0; i < indents; i++)
            {
                builder.Append("    ");
            }
        }

        public string Render(AssemblyAPIV assembly)
        {
            StringBuilder returnString = new StringBuilder();
            Render(assembly.GlobalNamespace, returnString);
            return returnString.ToString();
        }

        public void Render(AttributeAPIV a, StringBuilder builder)
        {
            builder.Append("[");
            RenderClass(builder, a.Type);

            if (a.ConstructorArgs.Any())
            {
                builder.Append("(");
                foreach (var arg in a.ConstructorArgs)
                {
                    RenderValue(builder, arg);
                    builder.Append(", ");
                }
                builder.Length -= 2;
                builder.Append(")");
            }

            builder.Append("]");
        }

        public void Render(EventAPIV e, StringBuilder builder, int indents = 0)
        {
            AppendIndents(builder, indents);
            //TODO: determine whether event is EventHandler or other type - and if it has type parameter(s)
            RenderKeyword(builder, e.Accessibility);
            builder.Append(" ");
            RenderSpecialName(builder, "event");
            builder.Append(" ");
            RenderClass(builder, "EventHandler");
            builder.Append(" ");
            RenderName(builder, e.Name);
            builder.Append(";");
        }

        public void Render(FieldAPIV f, StringBuilder builder, int indents = 0)
        {
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

            RenderType(builder, f.Type);
            builder.Append(" ");
            RenderName(builder, f.Name);

            if (f.IsConstant)
            {
                if (f.Type.Equals("string"))
                {
                    builder.Append(" = ");
                    RenderValue(builder, "\"" + f.Value + "\"");
                }
                else
                    builder.Append(" = ").Append(f.Value);
            }

            builder.Append(";");
        }

        public void Render(MethodAPIV m, StringBuilder builder, int indents = 0)
        {
            AppendIndents(builder, indents);
            if (m.Attributes.Any())
            {
                foreach (var attribute in m.Attributes)
                {
                    Render(attribute, builder);
                    RenderNewline(builder);
                    AppendIndents(builder, indents);
                }
            }

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

            if (m.ReturnType.Any())
            {
                if (m.ReturnType.Equals("void"))
                    RenderKeyword(builder, m.ReturnType);
                else
                    RenderType(builder, m.ReturnType);
                builder.Append(" ");
            }

            if (m.IsConstructor)
                RenderClass(builder, m.Name);
            else
                RenderName(builder, m.Name);

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
                foreach (ParameterAPIV p in m.Parameters)
                {
                    Render(p, builder);
                    builder.Append(", ");
                }
                builder.Length -= 2;
            }

            if (m.IsInterfaceMethod)
                builder.Append(");");
            else
                builder.Append(") { }");
        }

        public void Render(NamedTypeAPIV nt, StringBuilder builder, int indents = 0)
        {
            AppendIndents(builder, indents);
            RenderKeyword(builder, nt.Accessibility);
            builder.Append(" ");
            RenderSpecialName(builder, nt.Type);
            builder.Append(" ");

            indents++;

            switch (nt.Type)
            {
                case ("enum"):
                    RenderName(builder, nt.Name);
                    builder.Append(" ");

                    if (!nt.EnumUnderlyingType.Equals("int"))
                    {
                        builder.Append(": ");
                        RenderType(builder, nt.EnumUnderlyingType);
                        builder.Append(" ");
                    }
                    builder.Append("{");
                    RenderNewline(builder);

                    foreach (FieldAPIV f in nt.Fields)
                    {
                        AppendIndents(builder, indents);
                        builder.Append(f.Name).Append(" = ");
                        RenderValue(builder, f.Value.ToString());
                        builder.Append(",");
                        RenderNewline(builder);
                    }

                    AppendIndents(builder, indents - 1);
                    builder.Append("}");
                    break;

                case ("delegate"):
                    foreach (MethodAPIV m in nt.Methods)
                    {
                        if (m.Name.Equals("Invoke"))
                        {
                            RenderType(builder, m.ReturnType);
                            builder.Append(" ");
                            RenderName(builder, nt.Name);
                            builder.Append("(");

                            if (m.Parameters.Any())
                            {
                                foreach (ParameterAPIV p in m.Parameters)
                                {
                                    Render(p, builder);
                                    builder.Append(", ");
                                }
                                builder.Length -= 2;
                            }
                        }
                    }
                    builder.Append(") { }");
                    break;

                default:
                    RenderClass(builder, nt.Name);
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
                            RenderClass(builder, i);
                            builder.Append(", ");
                        }
                        builder.Length -= 2;
                        builder.Append(" ");
                    }
                    builder.Append("{");
                    RenderNewline(builder);

                    // add any types declared in this type's body
                    foreach (FieldAPIV f in nt.Fields)
                    {
                        Render(f, builder, indents);
                        RenderNewline(builder);
                    }
                    foreach (PropertyAPIV p in nt.Properties)
                    {
                        Render(p, builder, indents);
                        RenderNewline(builder);
                    }
                    foreach (EventAPIV e in nt.Events)
                    {
                        Render(e, builder, indents);
                        RenderNewline(builder);
                    }
                    foreach (MethodAPIV m in nt.Methods)
                    {
                        Render(m, builder, indents);
                        RenderNewline(builder);
                    }
                    foreach (NamedTypeAPIV n in nt.NamedTypes)
                    {
                        Render(n, builder, indents);
                        RenderNewline(builder);
                    }

                    AppendIndents(builder, indents - 1);
                    builder.Append("}");
                    break;
            }
        }

        public void Render(NamespaceAPIV ns, StringBuilder builder, int indents = 0)
        {
            if (ns.Name.Any())
            {
                AppendIndents(builder, indents);
                RenderSpecialName(builder, "namespace");
                builder.Append(" ");
                RenderName(builder, ns.Name);
                builder.Append(" {");
                RenderNewline(builder);
            }

            foreach (NamedTypeAPIV nt in ns.NamedTypes)
            {
                Render(nt, builder, indents + 1);
                RenderNewline(builder);
            }
            foreach (NamespaceAPIV n in ns.Namespaces)
            {
                if (ns.Name.Any())
                {
                    Render(n, builder, indents + 1);
                    RenderNewline(builder);
                }
                else
                {
                    Render(n, builder, indents);
                    RenderNewline(builder);
                }
            }

            if (ns.Name.Any())
            {
                AppendIndents(builder, indents);
                builder.Append("}");
            }
        }

        public void Render(ParameterAPIV p, StringBuilder builder, int indents = 0)
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

            RenderType(builder, p.Type);
            builder.Append(" ").Append(p.Name);
            if (p.HasExplicitDefaultValue)
            {
                if (p.Type.Equals("string"))
                {
                    builder.Append(" = ");
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

        public void Render(PropertyAPIV p, StringBuilder builder, int indents = 0)
        {
            AppendIndents(builder, indents);
            RenderKeyword(builder, p.Accessibility);
            builder.Append(" ");
            RenderType(builder, p.Type);
            builder.Append(" ");
            RenderName(builder, p.Name);
            builder.Append(" { ");
            RenderKeyword(builder, "get");
            builder.Append("; ");

            if (p.HasSetMethod)
            {
                RenderKeyword(builder, "set");
                builder.Append("; ");
            }

            builder.Append("}");
        }

        public void Render(TypeParameterAPIV tp, StringBuilder builder, int indents = 0)
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

        protected abstract void RenderPunctuation(StringBuilder s, string word);

        protected abstract void RenderClass(StringBuilder s, string word);

        protected abstract void RenderKeyword(StringBuilder s, string word);

        protected abstract void RenderName(StringBuilder s, string word);

        protected abstract void RenderNewline(StringBuilder s);

        protected abstract void RenderSpecialName(StringBuilder s, string word);

        protected abstract void RenderType(StringBuilder s, string word);

        protected abstract void RenderValue(StringBuilder s, string word);
    }
}
