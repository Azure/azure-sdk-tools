using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace TypeList
{
    public class Namespace
    {
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

            Collection<NamedType> namedTypes = new Collection<NamedType>();
            Collection<Namespace> namespaces = new Collection<Namespace>();

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

        public override string ToString()
        {
            StringBuilder returnString = new StringBuilder("");

            if (name.Length != 0)
                returnString = new StringBuilder("namespace " + name + " {\n\n");

            foreach (NamedType nt in namedTypes)
            {
                returnString.Append(nt.ToString() + "\n");
            }
            foreach(Namespace ns in namespaces)
            {
                returnString.Append(ns.ToString() + "\n");
            }

            if (name.Length != 0)
                returnString.Append("}\n");

            return returnString.ToString();
        }
    }
}