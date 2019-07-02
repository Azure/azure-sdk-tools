using System.Linq;
using System.Text;

namespace APIView
{
    public class TreeRendererAPIV
    {
        private static void AppendIndentsText(StringBuilder builder, int indents)
        {
            int indentSize = 4;
            string indent = new string(' ', indents * indentSize);
            builder.Append(indent);
        }
        
        public static string RenderText(AssemblyAPIV assembly)
        {
            StringBuilder returnString = new StringBuilder();
            RenderText(assembly.GlobalNamespace, returnString);
            return returnString.ToString();
        }

        internal static void RenderText(EventAPIV e, StringBuilder builder, int indents = 0)
        {
            AppendIndentsText(builder, indents);
            //TODO: determine whether event is EventHandler or other type - and if it has type parameter(s)
            builder.Append(e.Accessibility).Append(" event EventHandler ").Append(e.Name).Append(";");
        }

        internal static void RenderText(FieldAPIV f, StringBuilder builder, int indents = 0)
        {
            AppendIndentsText(builder, indents);
            builder.Append(f.Accessibility);

            if (f.IsStatic)
                builder.Append(" static");

            if (f.IsReadOnly)
                builder.Append(" readonly");

            if (f.IsVolatile)
                builder.Append(" volatile");

            if (f.IsConstant)
                builder.Append(" const");

            builder.Append(" ").Append(f.Type).Append(" ").Append(f.Name);

            if (f.IsConstant)
            {
                if (f.Value.GetType().Name.Equals("String"))
                    builder.Append(" = \"").Append(f.Value).Append("\"");
                else
                    builder.Append(" = ").Append(f.Value);
            }

            builder.Append(";");
        }

        internal static void RenderText(MethodAPIV m, StringBuilder builder, int indents = 0)
        {
            AppendIndentsText(builder, indents);
            if (m.Attributes.Any())
            {
                foreach (string attribute in m.Attributes)
                {
                    builder.Append("[").Append(attribute).Append("]").AppendLine();
                    AppendIndentsText(builder, indents);
                }
            }

            if (!m.IsInterfaceMethod)
                builder.Append(m.Accessibility).Append(" ");

            if (m.IsStatic)
                builder.Append("static ");
            if (m.IsVirtual)
                builder.Append("virtual ");
            if (m.IsSealed)
                builder.Append("sealed ");
            if (m.IsOverride)
                builder.Append("override ");
            if (m.IsAbstract && !m.IsInterfaceMethod)
                builder.Append("abstract ");
            if (m.IsExtern)
                builder.Append("extern ");

            if (m.ReturnType.Any())
                builder.Append(m.ReturnType).Append(" ");
            builder.Append(m.Name);

            if (m.TypeParameters.Any())
            {
                builder.Append("<");
                foreach (TypeParameterAPIV tp in m.TypeParameters)
                {
                    RenderText(tp, builder);
                    builder.Append(", ");
                }
                builder.Length -= 2;
                builder.Append(">");
            }

            builder.Append("(");
            if (m.Parameters.Any())
            {
                foreach (ParameterAPIV p in m.Parameters)
                {
                    RenderText(p, builder);
                    builder.Append(", ");
                }
                builder.Length -= 2;
            }

            if (m.IsInterfaceMethod)
                builder.Append(");");
            else
                builder.Append(") { }");
        }

        internal static void RenderText(NamedTypeAPIV nt, StringBuilder builder, int indents = 0)
        {
            AppendIndentsText(builder, indents);
            builder.Append(nt.Accessibility).Append(" ").Append(nt.Type).Append(" ");

            indents++;

            switch (nt.Type)
            {
                case ("enum"):
                    builder.Append(nt.Name).Append(" ");

                    if (!nt.EnumUnderlyingType.Equals("int"))
                        builder.Append(": ").Append(nt.EnumUnderlyingType).Append(" ");
                    builder.Append("{").AppendLine();

                    foreach (FieldAPIV f in nt.Fields)
                    {
                        AppendIndentsText(builder, indents);
                        builder.Append(f.Name).Append(" = ").Append(f.Value).Append(",").AppendLine();
                    }

                    AppendIndentsText(builder, indents - 1);
                    builder.Append("}");
                    break;

                case ("delegate"):
                    foreach (MethodAPIV m in nt.Methods)
                    {
                        if (m.Name.Equals("Invoke"))
                        {
                            builder.Append(m.ReturnType).Append(" ").Append(nt.Name).Append("(");
                            if (m.Parameters.Any())
                            {
                                foreach (ParameterAPIV p in m.Parameters)
                                {
                                    RenderText(p, builder);
                                    builder.Append(", ");
                                }
                                builder.Length -= 2;
                            }
                        }
                    }
                    builder.Append(") { }");
                    break;

                default:
                    builder.Append(nt.Name).Append(" ");

                    if (nt.TypeParameters.Any())
                    {
                        builder.Length -= 1;
                        builder.Append("<");
                        foreach (TypeParameterAPIV tp in nt.TypeParameters)
                        {
                            RenderText(tp, builder);
                            builder.Append(", ");
                        }
                        builder.Length -= 2;
                        builder.Append("> ");
                    }

                    // add any implemented types to string
                    if (nt.Implementations.Any())
                    {
                        builder.Append(": ");
                        foreach (var i in nt.Implementations)
                        {
                            builder.Append(i).Append(", ");
                        }
                        builder.Length -= 2;
                        builder.Append(" ");
                    }
                    builder.Append("{").AppendLine();

                    // add any types declared in this type's body
                    foreach (FieldAPIV f in nt.Fields)
                    {
                        RenderText(f, builder, indents);
                        builder.AppendLine();
                    }
                    foreach (PropertyAPIV p in nt.Properties)
                    {
                        RenderText(p, builder, indents);
                        builder.AppendLine();
                    }
                    foreach (EventAPIV e in nt.Events)
                    {
                        RenderText(e, builder, indents);
                        builder.AppendLine();
                    }
                    foreach (MethodAPIV m in nt.Methods)
                    {
                        RenderText(m, builder, indents);
                        builder.AppendLine();
                    }
                    foreach (NamedTypeAPIV n in nt.NamedTypes)
                    {
                        RenderText(n, builder, indents);
                        builder.AppendLine();
                    }

                    AppendIndentsText(builder, indents - 1);
                    builder.Append("}");
                    break;
            }
        }

        internal static void RenderText(NamespaceAPIV ns, StringBuilder builder, int indents = 0)
        {
            if (ns.Name.Any())
            {
                AppendIndentsText(builder, indents);
                builder.Append("namespace ").Append(ns.Name).Append(" {").AppendLine();
            }

            foreach (NamedTypeAPIV nt in ns.NamedTypes)
            {
                RenderText(nt, builder, indents + 1);
                builder.AppendLine();
            }
            foreach (NamespaceAPIV n in ns.Namespaces)
            {
                if (ns.Name.Any())
                {
                    RenderText(n, builder, indents + 1);
                    builder.AppendLine();
                }
                else
                {
                    RenderText(n, builder, indents);
                    builder.AppendLine();
                }
            }

            if (ns.Name.Any())
            {
                AppendIndentsText(builder, indents);
                builder.Append("}");
            }
        }

        internal static void RenderText(ParameterAPIV p, StringBuilder builder, int indents = 0)
        {
            if (p.Attributes.Any())
            {
                builder.Append("[");
                foreach (string attribute in p.Attributes)
                {
                    builder.Append(attribute).Append(", ");
                }
                builder.Length -= 2;
                builder.Append("] ").AppendLine();
                AppendIndentsText(builder, indents);
            }

            builder.Append(p.Type).Append(" ").Append(p.Name);
            if (p.HasExplicitDefaultValue)
            {
                if (p.Type.Equals("string"))
                    builder.Append(" = \"").Append(p.ExplicitDefaultValue).Append("\"");
                else
                    builder.Append(" = ");
                if (p.ExplicitDefaultValue == null)
                    builder.Append("null");
                else
                    builder.Append(p.ExplicitDefaultValue);
            }
        }

        internal static void RenderText(PropertyAPIV p, StringBuilder builder, int indents = 0)
        {
            AppendIndentsText(builder, indents);
            builder.Append(p.Accessibility).Append(" ").Append(p.Type).Append(" ").Append(p.Name).Append(" { get; ");

            if (p.HasSetMethod)
                builder.Append("set; ");

            builder.Append("}");
        }

        internal static void RenderText(TypeParameterAPIV tp, StringBuilder builder, int indents = 0)
        {
            if (tp.Attributes.Any())
            {
                builder.Append("[");
                foreach (string attribute in tp.Attributes)
                {
                    builder.Append(attribute).Append(", ");
                }
                builder.Length -= 2;
                builder.Append("] ").AppendLine();
                AppendIndentsText(builder, indents);
            }

            builder.Append(tp.Name);
        }

        private static void AppendIndentsHTML(StringBuilder builder, int indents)
        {
            builder.Append(Enumerable.Repeat("<span class=\"indent\"><\\span>", indents));
        }

        public static string RenderHTML(AssemblyAPIV assembly)
        {
            StringBuilder returnString = new StringBuilder();
            RenderHTML(assembly.GlobalNamespace, returnString);
            return returnString.ToString();
        }

        internal static void RenderHTML(EventAPIV e, StringBuilder builder, int indents = 0)
        {
            AppendIndentsHTML(builder, indents);
            //TODO: determine whether event is EventHandler or other type - and if it has type parameter(s)
            builder.Append(e.Accessibility).Append(" event EventHandler ").Append(e.Name).Append(";");
        }

        internal static void RenderHTML(FieldAPIV f, StringBuilder builder, int indents = 0)
        {
            AppendIndentsHTML(builder, indents);
            builder.Append(f.Accessibility);

            if (f.IsStatic)
                builder.Append(" static");

            if (f.IsReadOnly)
                builder.Append(" readonly");

            if (f.IsVolatile)
                builder.Append(" volatile");

            if (f.IsConstant)
                builder.Append(" const");

            builder.Append(" ").Append(f.Type).Append(" ").Append(f.Name);

            if (f.IsConstant)
            {
                if (f.Value.GetType().Name.Equals("String"))
                    builder.Append(" = \"").Append(f.Value).Append("\"");
                else
                    builder.Append(" = ").Append(f.Value);
            }

            builder.Append(";");
        }

        internal static void RenderHTML(MethodAPIV m, StringBuilder builder, int indents = 0)
        {
            AppendIndentsHTML(builder, indents);
            if (m.Attributes.Any())
            {
                foreach (string attribute in m.Attributes)
                {
                    builder.Append("[").Append(attribute).Append("]").Append("<br />");
                    AppendIndentsHTML(builder, indents);
                }
            }

            if (!m.IsInterfaceMethod)
                builder.Append(m.Accessibility).Append(" ");

            if (m.IsStatic)
                builder.Append("static ");
            if (m.IsVirtual)
                builder.Append("virtual ");
            if (m.IsSealed)
                builder.Append("sealed ");
            if (m.IsOverride)
                builder.Append("override ");
            if (m.IsAbstract && !m.IsInterfaceMethod)
                builder.Append("abstract ");
            if (m.IsExtern)
                builder.Append("extern ");

            if (m.ReturnType.Any())
                builder.Append(m.ReturnType).Append(" ");
            builder.Append(m.Name);

            if (m.TypeParameters.Any())
            {
                builder.Append("<");
                foreach (TypeParameterAPIV tp in m.TypeParameters)
                {
                    RenderHTML(tp, builder);
                    builder.Append(", ");
                }
                builder.Length -= 2;
                builder.Append(">");
            }

            builder.Append("(");
            if (m.Parameters.Any())
            {
                foreach (ParameterAPIV p in m.Parameters)
                {
                    RenderHTML(p, builder);
                    builder.Append(", ");
                }
                builder.Length -= 2;
            }

            if (m.IsInterfaceMethod)
                builder.Append(");");
            else
                builder.Append(") { }");
        }

        internal static void RenderHTML(NamedTypeAPIV nt, StringBuilder builder, int indents = 0)
        {
            AppendIndentsHTML(builder, indents);
            builder.Append(nt.Accessibility).Append(" ").Append(nt.Type).Append(" ");

            indents++;

            switch (nt.Type)
            {
                case ("enum"):
                    builder.Append(nt.Name).Append(" ");

                    if (!nt.EnumUnderlyingType.Equals("int"))
                        builder.Append(": ").Append(nt.EnumUnderlyingType).Append(" ");
                    builder.Append("{").Append("<br />");

                    foreach (FieldAPIV f in nt.Fields)
                    {
                        AppendIndentsHTML(builder, indents);
                        builder.Append(f.Name).Append(" = ").Append(f.Value).Append(",").Append("<br />");
                    }

                    AppendIndentsHTML(builder, indents - 1);
                    builder.Append("}");
                    break;

                case ("delegate"):
                    foreach (MethodAPIV m in nt.Methods)
                    {
                        if (m.Name.Equals("Invoke"))
                        {
                            builder.Append(m.ReturnType).Append(" ").Append(nt.Name).Append("(");
                            if (m.Parameters.Any())
                            {
                                foreach (ParameterAPIV p in m.Parameters)
                                {
                                    RenderHTML(p, builder);
                                    builder.Append(", ");
                                }
                                builder.Length -= 2;
                            }
                        }
                    }
                    builder.Append(") { }");
                    break;

                default:
                    builder.Append(nt.Name).Append(" ");

                    if (nt.TypeParameters.Any())
                    {
                        builder.Length -= 1;
                        builder.Append("<");
                        foreach (TypeParameterAPIV tp in nt.TypeParameters)
                        {
                            RenderHTML(tp, builder);
                            builder.Append(", ");
                        }
                        builder.Length -= 2;
                        builder.Append("> ");
                    }

                    // add any implemented types to string
                    if (nt.Implementations.Any())
                    {
                        builder.Append(": ");
                        foreach (var i in nt.Implementations)
                        {
                            builder.Append(i).Append(", ");
                        }
                        builder.Length -= 2;
                        builder.Append(" ");
                    }
                    builder.Append("{").Append("<br />");

                    // add any types declared in this type's body
                    foreach (FieldAPIV f in nt.Fields)
                    {
                        RenderHTML(f, builder, indents);
                        builder.Append("<br />");
                    }
                    foreach (PropertyAPIV p in nt.Properties)
                    {
                        RenderHTML(p, builder, indents);
                        builder.Append("<br />");
                    }
                    foreach (EventAPIV e in nt.Events)
                    {
                        RenderHTML(e, builder, indents);
                        builder.Append("<br />");
                    }
                    foreach (MethodAPIV m in nt.Methods)
                    {
                        RenderHTML(m, builder, indents);
                        builder.Append("<br />");
                    }
                    foreach (NamedTypeAPIV n in nt.NamedTypes)
                    {
                        RenderHTML(n, builder, indents);
                        builder.Append("<br />");
                    }

                    AppendIndentsHTML(builder, indents - 1);
                    builder.Append("}");
                    break;
            }
        }

        internal static void RenderHTML(NamespaceAPIV ns, StringBuilder builder, int indents = 0)
        {
            if (ns.Name.Any())
            {
                AppendIndentsHTML(builder, indents);
                builder.Append("namespace ").Append(ns.Name).Append(" {").Append("<br />");
            }

            foreach (NamedTypeAPIV nt in ns.NamedTypes)
            {
                RenderHTML(nt, builder, indents + 1);
                builder.Append("<br />");
            }
            foreach (NamespaceAPIV n in ns.Namespaces)
            {
                if (ns.Name.Any())
                {
                    RenderHTML(n, builder, indents + 1);
                    builder.Append("<br />");
                }
                else
                {
                    RenderHTML(n, builder, indents);
                    builder.Append("<br />");
                }
            }

            if (ns.Name.Any())
            {
                AppendIndentsHTML(builder, indents);
                builder.Append("}");
            }
        }

        internal static void RenderHTML(ParameterAPIV p, StringBuilder builder, int indents = 0)
        {
            if (p.Attributes.Any())
            {
                builder.Append("[");
                foreach (string attribute in p.Attributes)
                {
                    builder.Append(attribute).Append(", ");
                }
                builder.Length -= 2;
                builder.Append("] ").Append("<br />");
                AppendIndentsHTML(builder, indents);
            }

            builder.Append(p.Type).Append(" ").Append(p.Name);
            if (p.HasExplicitDefaultValue)
            {
                if (p.Type.Equals("string"))
                    builder.Append(" = \"").Append(p.ExplicitDefaultValue).Append("\"");
                else
                    builder.Append(" = ");
                if (p.ExplicitDefaultValue == null)
                    builder.Append("null");
                else
                    builder.Append(p.ExplicitDefaultValue);
            }
        }

        internal static void RenderHTML(PropertyAPIV p, StringBuilder builder, int indents = 0)
        {
            AppendIndentsHTML(builder, indents);
            builder.Append(p.Accessibility).Append(" ").Append(p.Type).Append(" ").Append(p.Name).Append(" { get; ");

            if (p.HasSetMethod)
                builder.Append("set; ");

            builder.Append("}");
        }

        internal static void RenderHTML(TypeParameterAPIV tp, StringBuilder builder, int indents = 0)
        {
            if (tp.Attributes.Any())
            {
                builder.Append("[");
                foreach (string attribute in tp.Attributes)
                {
                    builder.Append(attribute).Append(", ");
                }
                builder.Length -= 2;
                builder.Append("] ").Append("<br />");
                AppendIndentsHTML(builder, indents);
            }

            builder.Append(tp.Name);
        }
    }
}
