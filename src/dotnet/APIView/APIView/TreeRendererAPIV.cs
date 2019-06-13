using System;
using System.Collections.Generic;
using System.Text;

namespace APIView
{
    public class TreeRendererAPIV
    {
        private const int IndentSize = 4;

        private void AppendIndents(StringBuilder builder, int indents)
        {
            string indent = new string(' ', indents * IndentSize);
            builder.Append(indent);
        }

        public string Render(AssemblyAPIV assembly)
        {
            StringBuilder returnString = new StringBuilder();
            Render(assembly.GlobalNamespace, returnString);
            return returnString.ToString();
        }

        private void Render(EventAPIV e, StringBuilder builder, int indents = 0)
        {
            AppendIndents(builder, indents);
            //TODO: determine whether event is EventHandler or other type - and if it has type parameter(s)
            builder.Append("public event EventHandler ").Append(e.Name).Append(";");
        }

        private void Render(FieldAPIV f, StringBuilder builder, int indents = 0)
        {
            AppendIndents(builder, indents);
            builder.Append("public");

            if (f.IsConstant)
                builder.Append(" const");

            if (f.IsStatic)
                builder.Append(" static");

            if (f.IsReadOnly)
                builder.Append(" readonly");

            if (f.IsVolatile)
                builder.Append(" volatile");

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

        private void Render(MethodAPIV m, StringBuilder builder, int indents = 0)
        {
            AppendIndents(builder, indents);
            if (!m.Attributes.IsEmpty)
            {
                builder.Append("[").Append(m.Attributes[0].AttributeClass.Name).Append("]");
                builder.AppendLine();
                AppendIndents(builder, indents);
            }

            if (!m.IsInterfaceMethod)
                builder.Append("public");

            if (m.IsStatic)
                builder.Append(" static");
            if (m.IsVirtual)
                builder.Append(" virtual");
            if (m.IsSealed)
                builder.Append(" sealed");
            if (m.IsOverride)
                builder.Append(" override");
            if (m.IsAbstract && !m.IsInterfaceMethod)
                builder.Append(" abstract");
            if (m.IsExtern)
                builder.Append(" extern");

            builder.Append(" ").Append(m.ReturnType).Append(" ").Append(m.Name);
            if (m.TypeParameters.Length != 0)
            {
                builder.Append("<");
                foreach (TypeParameterAPIV tp in m.TypeParameters)
                {
                    Render(tp, builder);
                    builder.Append(", ");
                }
                builder.Length = builder.Length - 2;
                builder.Append(">");
            }

            builder.Append("(");
            if (m.Parameters.Length != 0)
            {
                foreach (ParameterAPIV p in m.Parameters)
                {
                    Render(p, builder);
                    builder.Append(", ");
                }
                builder.Length = builder.Length - 2;
            }

            if (m.IsInterfaceMethod)
                builder.Append(");");
            else
                builder.Append(") { }");
        }

        private void Render(NamedTypeAPIV nt, StringBuilder builder, int indents = 0)
        {
            AppendIndents(builder, indents);
            builder.Append("public ").Append(nt.Type).Append(" ").Append(nt.Name).Append(" ");

            // add any implemented types to string
            if (nt.Implementations.Length > 0)
            {
                builder.Append(": ");
                foreach (var i in nt.Implementations)
                {
                    builder.Append(i).Append(", ");
                }
                builder.Length = builder.Length - 2;
                builder.Append(" ");
            }
            builder.Append("{");
            builder.AppendLine();

            indents++;

            // add any types declared in this type's body
            foreach (FieldAPIV f in nt.Fields)
            {
                Render(f, builder, indents);
                builder.AppendLine();
            }
            foreach (PropertyAPIV p in nt.Properties)
            {
                Render(p, builder, indents);
                builder.AppendLine();
            }
            foreach (EventAPIV e in nt.Events)
            {
                Render(e, builder, indents);
                builder.AppendLine();
            }
            foreach (MethodAPIV m in nt.Methods)
            {
                Render(m, builder, indents);
                builder.AppendLine();
            }
            foreach (NamedTypeAPIV n in nt.NamedTypes)
            {
                Render(n, builder, indents);
                builder.AppendLine();
            }

            AppendIndents(builder, indents - 1);
            builder.Append("}");
        }

        private void Render(NamespaceAPIV ns, StringBuilder builder, int indents = 0)
        {
            if (ns.Name.Length != 0)
            {
                AppendIndents(builder, indents);
                builder.Append("namespace ").Append(ns.Name).Append(" {");
                builder.AppendLine();
            }

            foreach (NamedTypeAPIV nt in ns.NamedTypes)
            {
                Render(nt, builder, indents + 1);
                builder.AppendLine();
            }
            foreach (NamespaceAPIV n in ns.Namespaces)
            {
                if (ns.Name.Length != 0)
                {
                    Render(n, builder, indents + 1);
                    builder.AppendLine();
                }
                else
                {
                    Render(n, builder, indents);
                    builder.AppendLine();
                }
            }

            if (ns.Name.Length != 0)
            {
                AppendIndents(builder, indents);
                builder.Append("}");
            }
        }

        private void Render(ParameterAPIV p, StringBuilder builder, int indents = 0)
        {
            builder.Append(p.Type).Append(" ").Append(p.Name);
            if (p.HasExplicitDefaultValue)
            {
                if (p.Type.Equals("string"))
                    builder.Append(" = \"").Append(p.ExplicitDefaultValue).Append("\"");
                else
                    builder.Append(" = ").Append(p.ExplicitDefaultValue);
            }
        }

        private void Render(PropertyAPIV p, StringBuilder builder, int indents = 0)
        {
            AppendIndents(builder, indents);
            builder.Append("public ").Append(p.Type).Append(" ").Append(p.Name).Append(" { get; ");

            if (p.HasSetMethod)
                builder.Append("set; ");

            builder.Append("}");
        }

        private void Render(TypeParameterAPIV tp, StringBuilder builder, int indents = 0)
        {
            if (tp.Attributes.Length != 0)
                builder.Append(tp.Attributes).Append(" ");

            builder.Append(tp.Name);
        }
    }
}
