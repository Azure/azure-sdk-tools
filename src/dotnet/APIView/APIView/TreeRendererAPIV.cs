using System;
using System.Linq;
using System.Text;

namespace APIView
{

    public abstract class Renderer
    {
        protected abstract void RenderKeyword(StringBuilder s, string kw);
    }


    public class HtmlRendered
    {

    }

    public class TreeRendererAPIV
    {
        private static void AppendIndents(StringBuilder builder, int indents)
        {
            for (int i = 0; i < indents; i++)
            {
                builder.Append("    ");
            }
        }
        
        public static string RenderText(AssemblyAPIV assembly)
        {
            StringBuilder returnString = new StringBuilder();
            RenderText(assembly.GlobalNamespace, returnString);
            return returnString.ToString();
        }

        internal static void RenderText(AttributeAPIV a, StringBuilder builder)
        {
            builder.Append("[");
            builder.Append(a.Type);
            
            if (a.ConstructorArgs.Any())
            {
                builder.Append("(");
                foreach (var arg in a.ConstructorArgs)
                {
                    builder.Append(arg);
                    builder.Append(", ");
                }
                builder.Length -= 2;
                builder.Append(")");
            }
            builder.Append("]");
        }

        internal static void RenderText(EventAPIV e, StringBuilder builder, int indents = 0)
        {
            AppendIndents(builder, indents);
            //TODO: determine whether event is EventHandler or other type - and if it has type parameter(s)
            builder.Append(e.Accessibility).Append(" event EventHandler ").Append(e.Name).Append(";");
        }

        internal static void RenderText(FieldAPIV f, StringBuilder builder, int indents = 0)
        {
            AppendIndents(builder, indents);
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
                if (f.Type.Equals("string"))
                    builder.Append(" = \"").Append(f.Value).Append("\"");
                else
                    builder.Append(" = ").Append(f.Value);
            }

            builder.Append(";");
        }

        internal static void RenderText(MethodAPIV m, StringBuilder builder, int indents = 0)
        {
            AppendIndents(builder, indents);
            if (m.Attributes.Any())
            {
                foreach (var attribute in m.Attributes)
                {
                    builder.Append(attribute).AppendLine();
                    AppendIndents(builder, indents);
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
            AppendIndents(builder, indents);
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
                        AppendIndents(builder, indents);
                        builder.Append(f.Name).Append(" = ").Append(f.Value).Append(",").AppendLine();
                    }

                    AppendIndents(builder, indents - 1);
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

                    AppendIndents(builder, indents - 1);
                    builder.Append("}");
                    break;
            }
        }

        internal static void RenderText(NamespaceAPIV ns, StringBuilder builder, int indents = 0)
        {
            if (ns.Name.Any())
            {
                AppendIndents(builder, indents);
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
                AppendIndents(builder, indents);
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
                AppendIndents(builder, indents);
            }

            builder.Append(p.Type).Append(" ").Append(p.Name);
            if (p.HasExplicitDefaultValue)
            {
                if (p.Type.Equals("string"))
                    builder.Append(" = \"").Append(p.ExplicitDefaultValue).Append("\"");

                else
                {
                    builder.Append(" = ");
                    if (p.ExplicitDefaultValue == null)
                        builder.Append("null");
                    else
                        builder.Append(p.ExplicitDefaultValue);
                }
            }
        }

        internal static void RenderText(PropertyAPIV p, StringBuilder builder, int indents = 0)
        {
            AppendIndents(builder, indents);
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
                AppendIndents(builder, indents);
            }

            builder.Append(tp.Name);
        }

        public static string RenderHTML(AssemblyAPIV assembly)
        {
            StringBuilder returnString = new StringBuilder();
            RenderHTML(assembly.GlobalNamespace, returnString);
            return returnString.ToString();
        }

        internal static void RenderHTML(AttributeAPIV a, StringBuilder builder)
        {
            builder.Append("[");
            MakeClass(builder, a.Type);

            if (a.ConstructorArgs.Any())
            {
                builder.Append("(");
                foreach (var arg in a.ConstructorArgs)
                {
                    MakeValue(builder, arg);
                    builder.Append(", ");
                }
                builder.Length -= 2;
                builder.Append(")");
            }

            builder.Append("]");
        }

        internal static void RenderHTML(EventAPIV e, StringBuilder builder, int indents = 0)
        {
            AppendIndents(builder, indents);
            //TODO: determine whether event is EventHandler or other type - and if it has type parameter(s)
            MakeKeyword(builder, e.Accessibility);
            builder.Append(" ");
            MakeKeyword(builder, "event");
            builder.Append(" ");
            MakeClass(builder, "EventHandler");
            builder.Append(" ");
            MakeName(builder, e.Name);
            builder.Append(";");
        }

        internal static void RenderHTML(FieldAPIV f, StringBuilder builder, int indents = 0)
        {
            AppendIndents(builder, indents);
            MakeKeyword(builder, f.Accessibility);
            builder.Append(" ");

            if (f.IsStatic)
            {
                MakeKeyword(builder, "static");
                builder.Append(" ");
            }
            if (f.IsReadOnly)
            {
                MakeKeyword(builder, "readonly");
                builder.Append(" ");
            }
            if (f.IsVolatile)
            {
                MakeKeyword(builder, "volatile");
                builder.Append(" ");
            }
            if (f.IsConstant)
            {
                MakeKeyword(builder, "const");
                builder.Append(" ");
            }

            MakeType(builder, f.Type);
            builder.Append(" ");
            MakeName(builder, f.Name);

            if (f.IsConstant)
            {
                if (f.Type.Equals("string"))
                {
                    builder.Append(" = ");
                    MakeValue(builder, "\"" + f.Value + "\"");
                }
                else
                    builder.Append(" = ").Append(f.Value);
            }

            builder.Append(";");
        }

        internal static void RenderHTML(MethodAPIV m, StringBuilder builder, int indents = 0)
        {
            AppendIndents(builder, indents);
            if (m.Attributes.Any())
            {
                foreach (var attribute in m.Attributes)
                {
                    RenderHTML(attribute, builder);
                    builder.Append("<br />");
                    AppendIndents(builder, indents);
                }
            }

            if (!m.IsInterfaceMethod)
            {
                MakeKeyword(builder, m.Accessibility);
                builder.Append(" ");
            }

            if (m.IsStatic)
            {
                MakeKeyword(builder, "static");
                builder.Append(" ");
            }
            if (m.IsVirtual)
            {
                MakeKeyword(builder, "virtual");
                builder.Append(" ");
            }
            if (m.IsSealed)
            {
                MakeKeyword(builder, "sealed");
                builder.Append(" ");
            }
            if (m.IsOverride)
            {
                MakeKeyword(builder, "override");
                builder.Append(" ");
            }
            if (m.IsAbstract && !m.IsInterfaceMethod)
            {
                MakeKeyword(builder, "abstract");
                builder.Append(" ");
            }
            if (m.IsExtern)
            {
                MakeKeyword(builder, "extern");
                builder.Append(" ");
            }

            if (m.ReturnType.Any())
            {
                MakeType(builder, m.ReturnType);
                builder.Append(" ");
            }

            // Indicates method is a constructor.
            if (m.Name.Equals(m.Parent))
                MakeClass(builder, m.Name);
            else
                MakeName(builder, m.Name);

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
            AppendIndents(builder, indents);
            MakeKeyword(builder, nt.Accessibility);
            builder.Append(" ");
            MakeSpecialName(builder, nt.Type);
            builder.Append(" ");

            indents++;

            switch (nt.Type)
            {
                case ("enum"):
                    MakeName(builder, nt.Name);
                    builder.Append(" ");

                    if (!nt.EnumUnderlyingType.Equals("int"))
                    {
                        builder.Append(": ");
                        MakeType(builder, nt.EnumUnderlyingType);
                        builder.Append(" ");
                    }
                    builder.Append("{").Append("<br />");

                    foreach (FieldAPIV f in nt.Fields)
                    {
                        AppendIndents(builder, indents);
                        builder.Append(f.Name).Append(" = ");
                        MakeValue(builder, f.Value.ToString());
                        builder.Append(",").Append("<br />");
                    }

                    AppendIndents(builder, indents - 1);
                    builder.Append("}");
                    break;

                case ("delegate"):
                    foreach (MethodAPIV m in nt.Methods)
                    {
                        if (m.Name.Equals("Invoke"))
                        {
                            MakeType(builder, m.ReturnType);
                            builder.Append(" ");
                            MakeName(builder, nt.Name);
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
                        }
                    }
                    builder.Append(") { }");
                    break;

                default:
                    MakeClass(builder, nt.Name);
                    builder.Append(" ");

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
                            MakeClass(builder, i);
                            builder.Append(", ");
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

                    AppendIndents(builder, indents - 1);
                    builder.Append("}");
                    break;
            }
        }

        internal static void RenderHTML(NamespaceAPIV ns, StringBuilder builder, int indents = 0)
        {
            if (ns.Name.Any())
            {
                AppendIndents(builder, indents);
                MakeSpecialName(builder, "namespace");
                builder.Append(" ");
                MakeName(builder, ns.Name);
                builder.Append(" {").Append("<br />");
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
                AppendIndents(builder, indents);
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
                    MakeName(builder, attribute);
                    builder.Append(", ");
                }
                builder.Length -= 2;
                builder.Append("] ").Append("<br />");
                AppendIndents(builder, indents);
            }

            MakeType(builder, p.Type);
            builder.Append(" ").Append(p.Name);
            if (p.HasExplicitDefaultValue)
            {
                if (p.Type.Equals("string"))
                {
                    builder.Append(" = ");
                    MakeValue(builder, "\"" + p.ExplicitDefaultValue.ToString() + "\"");
                }

                else
                {
                    builder.Append(" = ");
                    if (p.ExplicitDefaultValue == null)
                        MakeSpecialName(builder, "null");
                    else
                        MakeValue(builder, p.ExplicitDefaultValue.ToString());
                }
            }
        }

        internal static void RenderHTML(PropertyAPIV p, StringBuilder builder, int indents = 0)
        {
            AppendIndents(builder, indents);
            MakeKeyword(builder, p.Accessibility);
            builder.Append(" ");
            MakeType(builder, p.Type);
            builder.Append(" ").Append(p.Name).Append(" { ");
            MakeKeyword(builder, "get");
            builder.Append("; ");

            if (p.HasSetMethod)
            {
                MakeKeyword(builder, "set");
                builder.Append("; ");
            }

            builder.Append("}");
        }

        internal static void RenderHTML(TypeParameterAPIV tp, StringBuilder builder, int indents = 0)
        {
            if (tp.Attributes.Any())
            {
                builder.Append("[");
                foreach (string attribute in tp.Attributes)
                {
                    MakeName(builder, attribute);
                    builder.Append(", ");
                }
                builder.Length -= 2;
                builder.Append("] ").Append("<br />");
                AppendIndents(builder, indents);
            }

            builder.Append(tp.Name);
        }

        private static void MakeClass(StringBuilder builder, string word)
        {
            builder.Append("<font class=\"class\">").Append(word).Append("</font>");
        }

        private static void MakeKeyword(StringBuilder builder, string word)
        {
            builder.Append("<font class=\"keyword\">").Append(word).Append("</font>");
        }

        private static void MakeName(StringBuilder builder, string word)
        {
            builder.Append("<font class=\"name\">").Append(word).Append("</font>");
        }

        private static void MakeSpecialName(StringBuilder builder, string word)
        {
            builder.Append("<font class=\"specialName\">").Append(word).Append("</font>");
        }

        private static void MakeType(StringBuilder builder, string word)
        {
            builder.Append("<font class=\"type\">").Append(word).Append("</font>");
        }

        private static void MakeValue(StringBuilder builder, string word)
        {
            builder.Append("<font class=\"value\">").Append(word).Append("</font>");
        }
    }
}
