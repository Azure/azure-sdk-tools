using System;
using System.Collections.Generic;
using System.Text;

namespace APIView
{
    public class TreeRendererAPIV
    {
        private const int IndentSize = 4;

        public string Render(AssemblyAPIV assembly)
        {
            StringBuilder returnString = new StringBuilder();
            Render(assembly.GlobalNamespace, returnString);
            return returnString.ToString();
        }

        private void Render(EventAPIV e, StringBuilder returnString, int indents = 0)
        {
            string indent = new string(' ', indents * IndentSize);

            //TODO: determine whether event is EventHandler or other type - and if it has type parameter(s)
            returnString.Append(indent + "public event EventHandler " + e.Name + ";");
        }

        private void Render(FieldAPIV f, StringBuilder returnString, int indents = 0)
        {
            string indent = new string(' ', indents * IndentSize);

            returnString.Append(indent + "public");

            if (f.IsConstant)
                returnString.Append(" const");

            if (f.IsStatic)
                returnString.Append(" static");

            if (f.IsReadOnly)
                returnString.Append(" readonly");

            if (f.IsVolatile)
                returnString.Append(" volatile");

            returnString.Append(" " + f.Type + " " + f.Name);

            if (f.IsConstant)
            {
                if (f.Value.GetType().Name.Equals("String"))
                    returnString.Append(" = \"" + f.Value + "\"");
                else
                    returnString.Append(" = " + f.Value);
            }

            returnString.Append(";");
        }

        private void Render(MethodAPIV m, StringBuilder returnString, int indents = 0)
        {
            string indent = new string(' ', indents * IndentSize);

            bool interfaceMethod = m.Symbol.ContainingType.TypeKind.ToString().ToLower().Equals("interface");

            returnString.Append(indent);
            if (!m.Attributes.IsEmpty)
                returnString.Append("[" + m.Attributes[0].AttributeClass.Name + "]\n" + indent);

            if (!interfaceMethod)
                returnString.Append("public");

            if (m.IsStatic)
                returnString.Append(" static");
            if (m.IsVirtual)
                returnString.Append(" virtual");
            if (m.IsSealed)
                returnString.Append(" sealed");
            if (m.IsOverride)
                returnString.Append(" override");
            if (m.IsAbstract && !interfaceMethod)
                returnString.Append(" abstract");
            if (m.IsExtern)
                returnString.Append(" extern");

            returnString.Append(" " + m.ReturnType + " " + m.Name);
            if (m.TypeParameters.Length != 0)
            {
                returnString.Append("<");
                foreach (TypeParameterAPIV tp in m.TypeParameters)
                {
                    Render(tp, returnString);
                    returnString.Append(", ");
                }
                returnString.Length = returnString.Length - 2;
                returnString.Append(">");
            }

            returnString.Append("(");
            if (m.Parameters.Length != 0)
            {
                foreach (ParameterAPIV p in m.Parameters)
                {
                    Render(p, returnString);
                    returnString.Append(", ");
                }
                returnString.Length = returnString.Length - 2;
            }

            if (interfaceMethod)
                returnString.Append(");");
            else
                returnString.Append(") { }");
        }

        private void Render(NamedTypeAPIV nt, StringBuilder returnString, int indents = 0)
        {
            string indent = new string(' ', indents * IndentSize);

            returnString.Append(indent + "public " + nt.Type + " " + nt.Name + " ");

            // add any implemented types to string
            if (nt.Implementations.Length > 0)
            {
                returnString.Append(": ");
                foreach (var i in nt.Implementations)
                {
                    returnString.Append(i + ", ");
                }
                returnString.Length = returnString.Length - 2;
                returnString.Append(" ");
            }
            returnString.Append("{\n");

            // add any types declared in this type's body
            foreach (FieldAPIV f in nt.Fields)
            {
                Render(f, returnString, indents + 1);
                returnString.AppendLine();
            }
            foreach (PropertyAPIV p in nt.Properties)
            {
                Render(p, returnString, indents + 1);
                returnString.AppendLine();
            }
            foreach (EventAPIV e in nt.Events)
            {
                Render(e, returnString, indents + 1);
                returnString.AppendLine();
            }
            foreach (MethodAPIV m in nt.Methods)
            {
                Render(m, returnString, indents + 1);
                returnString.AppendLine();
            }
            foreach (NamedTypeAPIV n in nt.NamedTypes)
            {
                Render(n, returnString, indents + 1);
                returnString.AppendLine();
            }

            returnString.Append(indent + "}");
        }

        private void Render(NamespaceAPIV ns, StringBuilder returnString, int indents = 0)
        {
            string indent = new string(' ', indents * IndentSize);

            if (ns.Name.Length != 0)
                returnString.Append(indent + "namespace " + ns.Name + " {\n");

            foreach (NamedTypeAPIV nt in ns.NamedTypes)
            {
                Render(nt, returnString, indents + 1);
                returnString.AppendLine();
            }
            foreach (NamespaceAPIV n in ns.Namespaces)
            {
                if (ns.Name.Length != 0)
                {
                    Render(n, returnString, indents + 1);
                    returnString.AppendLine();
                }
                else
                {
                    Render(n, returnString, indents);
                    returnString.AppendLine();
                }
            }

            if (ns.Name.Length != 0)
                returnString.Append(indent + "}");
        }

        private void Render(ParameterAPIV p, StringBuilder returnString, int indents = 0)
        {
            returnString.Append(p.Type + " " + p.Name);
            if (p.HasExplicitDefaultValue)
            {
                if (p.Type.Equals("string"))
                    returnString.Append(" = \"" + p.ExplicitDefaultValue + "\"");
                else
                    returnString.Append(" = " + p.ExplicitDefaultValue);
            }
        }

        private void Render(PropertyAPIV p, StringBuilder returnString, int indents = 0)
        {
            string indent = new string(' ', indents * IndentSize);

            returnString.Append(indent + "public " + p.Type + " " + p.Name + " { get; ");
            if (p.HasSetMethod)
                returnString.Append("set; ");

            returnString.Append("}");
        }

        private void Render(TypeParameterAPIV tp, StringBuilder returnString, int indents = 0)
        {
            if (tp.Attributes.Length != 0)
                returnString.Append(tp.Attributes + " ");

            returnString.Append(tp.Name);
        }
    }
}
