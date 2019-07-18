using System.ComponentModel.Design;
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
        }

        public void Render(EventAPIV e, StringBuilder builder, int indents = 0)
        {
            AppendIndents(builder, indents);
            //TODO: determine whether event is EventHandler or other type - and if it has type parameter(s)
            RenderKeyword(builder, e.Accessibility);
            builder.Append(" ");
            RenderKeyword(builder, "event");
            builder.Append(" ");
            Render(e.Type, builder);
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

            Render(f.Type, builder);
            builder.Append(" ");
            RenderName(builder, f.Name);

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

            if (m.ReturnType != null) //(m.ReturnType.Type != TypeReference.TypeName.NullType)
            {
                Render(m.ReturnType, builder);
                builder.Append(" ");
            }

            if (m.IsConstructor)
                RenderConstructor(builder, m);
            else
                RenderMethod(builder, m);

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

            if (m.IsInterfaceMethod || m.IsAbstract)
                builder.Append(");");
            else
                builder.Append(") { }");
        }

        public void Render(NamedTypeAPIV nt, StringBuilder builder, int indents = 0)
        {
            AppendIndents(builder, indents);
            RenderKeyword(builder, nt.Accessibility);
            builder.Append(" ");
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
                            Render(m.ReturnType, builder);
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
            if (ns.Name != "<global namespace>")
            {
                AppendIndents(builder, indents);
                RenderKeyword(builder, "namespace");
                builder.Append(" ");
                RenderNamespace(builder, ns);
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

            if (ns.Name != "<global namespace>")
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

        public void Render(PropertyAPIV p, StringBuilder builder, int indents = 0)
        {
            AppendIndents(builder, indents);
            RenderKeyword(builder, p.Accessibility);
            builder.Append(" ");
            Render(p.Type, builder);
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

        public void Render(TypeReferenceAPIV type, StringBuilder builder)
        {
            if (type == null || type?.Tokens == null)
                return;
            foreach (var token in type.Tokens)
            {
                RenderToken(builder, token);
            }
        }

        protected abstract void RenderClassDefinition(StringBuilder builder, NamedTypeAPIV nt);

        protected abstract void RenderEnumDefinition(StringBuilder builder, NamedTypeAPIV nt);

        protected abstract void RenderPunctuation(StringBuilder builder, string word);

        protected abstract void RenderEnum(StringBuilder builder, TokenAPIV t);

        protected abstract void RenderClass(StringBuilder builder, TokenAPIV t);

        protected abstract void RenderConstructor(StringBuilder builder, MethodAPIV m);

        protected abstract void RenderKeyword(StringBuilder builder, string word);

        protected abstract void RenderMethod(StringBuilder builder, MethodAPIV m);

        protected abstract void RenderName(StringBuilder builder, string word);

        protected abstract void RenderNamespace(StringBuilder builder, NamespaceAPIV ns);

        protected abstract void RenderNewline(StringBuilder builder);

        protected abstract void RenderSpecialName(StringBuilder builder, string word);

        protected abstract void RenderToken(StringBuilder builder, TokenAPIV t);

        protected abstract void RenderType(StringBuilder builder, string word);

        protected abstract void RenderValue(StringBuilder builder, string word);
    }
}
