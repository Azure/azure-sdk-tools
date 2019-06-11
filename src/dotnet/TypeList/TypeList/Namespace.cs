using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace TypeList
{
    public class Namespace
    {
        private const int indentSize = 4;

        private readonly string Name;

        private readonly ImmutableArray<NamedType> NamedTypes;
        private readonly ImmutableArray<Namespace> Namespaces;

        /// <summary>
        /// Construct a new namespace instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the namespace.</param>
        public Namespace(INamespaceSymbol symbol)
        {
            this.Name = symbol.Name;

            List<NamedType> namedTypes = new List<NamedType>();
            List<Namespace> namespaces = new List<Namespace>();

            foreach (var memberSymbol in symbol.GetMembers().OfType<INamespaceOrTypeSymbol>())
            {
                if (memberSymbol.DeclaredAccessibility != Accessibility.Public) continue;

                if (memberSymbol is INamedTypeSymbol nt) namedTypes.Add(new NamedType(nt));

                else if (memberSymbol is INamespaceSymbol ns) namespaces.Add(new Namespace(ns));
            }

            this.NamedTypes = namedTypes.ToImmutableArray();
            this.Namespaces = namespaces.ToImmutableArray();
        }

        public string GetName()
        {
            return Name;
        }

        public ImmutableArray<NamedType> GetNamedTypes()
        {
            return NamedTypes;
        }

        public ImmutableArray<Namespace> GetNamespaces()
        {
            return Namespaces;
        }

        public string RenderNamespace(int indents = 0)
        {
            string indent = new string(' ', indents * indentSize);

            StringBuilder returnString = new StringBuilder("");

            if (Name.Length != 0)
                returnString = new StringBuilder(indent + "namespace " + Name + " {\n");

            foreach (NamedType nt in NamedTypes)
            {
                returnString.Append(indent + nt.RenderNamedType(indents + 1) + "\n");
            }
            foreach (Namespace ns in Namespaces)
            {
                if (Name.Length != 0)
                    returnString.Append(indent + ns.RenderNamespace(indents + 1) + "\n");
                else
                    returnString.Append(indent + ns.RenderNamespace(indents) + "\n");
            }

            if (Name.Length != 0)
                returnString.Append(indent + "}");

            return returnString.ToString();
        }

        public override string ToString()
        {
            StringBuilder returnString = new StringBuilder("");

            if (Name.Length != 0)
                returnString = new StringBuilder("namespace " + Name + " {\n");

            foreach (NamedType nt in NamedTypes)
            {
                returnString.Append(nt.ToString() + "\n");
            }
            foreach(Namespace ns in Namespaces)
            {
                returnString.Append(ns.ToString() + "\n");
            }

            if (Name.Length != 0)
                returnString.Append("}");

            return returnString.ToString();
        }
    }
}