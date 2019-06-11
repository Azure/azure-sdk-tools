using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace TypeList
{
    public class Namespace
    {
        private const int INDENT_SIZE = 4;

        private readonly string name;

        private readonly ImmutableArray<NamedType> namedTypes;
        private readonly ImmutableArray<Namespace> namespaces;

        /// <summary>
        /// Construct a new Namespace instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the namespace.</param>
        public Namespace(INamespaceSymbol symbol)
        {
            this.name = symbol.Name;

            List<NamedType> namedTypes = new List<NamedType>();
            List<Namespace> namespaces = new List<Namespace>();

            foreach (var memberSymbol in symbol.GetMembers().OfType<INamespaceOrTypeSymbol>())
            {
                if (memberSymbol.DeclaredAccessibility != Accessibility.Public) continue;

                if (memberSymbol is INamedTypeSymbol nt) namedTypes.Add(new NamedType(nt));

                else if (memberSymbol is INamespaceSymbol ns) namespaces.Add(new Namespace(ns));
            }

            this.namedTypes = namedTypes.ToImmutableArray();
            this.namespaces = namespaces.ToImmutableArray();
        }

        public string GetName()
        {
            return name;
        }

        public ImmutableArray<NamedType> GetNamedTypes()
        {
            return namedTypes;
        }

        public ImmutableArray<Namespace> GetNamespaces()
        {
            return namespaces;
        }

        public string RenderNamespace(int indents = 0)
        {
            string indent = new string(' ', indents * INDENT_SIZE);

            StringBuilder returnString = new StringBuilder("");

            if (name.Length != 0)
                returnString = new StringBuilder(indent + "namespace " + name + " {\n");

            foreach (NamedType nt in namedTypes)
            {
                returnString.Append(indent + nt.RenderNamedType(indents + 1) + "\n");
            }
            foreach (Namespace ns in namespaces)
            {
                if (name.Length != 0)
                    returnString.Append(indent + ns.RenderNamespace(indents + 1) + "\n");
                else
                    returnString.Append(indent + ns.RenderNamespace(indents) + "\n");
            }

            if (name.Length != 0)
                returnString.Append(indent + "}");

            return returnString.ToString();
        }

        public override string ToString()
        {
            StringBuilder returnString = new StringBuilder("");

            if (name.Length != 0)
                returnString = new StringBuilder("namespace " + name + " {\n");

            foreach (NamedType nt in namedTypes)
            {
                returnString.Append(nt.ToString() + "\n");
            }
            foreach(Namespace ns in namespaces)
            {
                returnString.Append(ns.ToString() + "\n");
            }

            if (name.Length != 0)
                returnString.Append("}");

            return returnString.ToString();
        }
    }
}