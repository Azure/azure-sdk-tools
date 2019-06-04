using Microsoft.CodeAnalysis;
using System.Collections.ObjectModel;
using System.Linq;

namespace TypeList
{
    internal class Namespace
    {
        private readonly INamespaceSymbol symbol;

        private readonly Collection<NamedType> namedTypes = new Collection<NamedType>();
        private readonly Collection<Namespace> namespaces = new Collection<Namespace>();

        /// <summary>
        /// Construct a new Namespace instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the namespace.</param>
        public Namespace(INamespaceSymbol symbol)
        {
            this.symbol = symbol;

            foreach (var memberSymbol in symbol.GetMembers().OfType<INamespaceOrTypeSymbol>())
            {
                if (memberSymbol.DeclaredAccessibility != Accessibility.Public) continue;

                if (memberSymbol is INamedTypeSymbol nt) this.namedTypes.Add(new NamedType(nt));

                else if (memberSymbol is INamespaceSymbol ns) this.namespaces.Add(new Namespace(ns));
            }
        }

        public override string ToString()
        {
            string returnString = "Namespace: " + symbol + "\n";

            foreach (NamedType nt in namedTypes)
            {
                returnString += nt.ToString();
            }
            foreach(Namespace ns in namespaces)
            {
                returnString += ns.ToString();
            }

            return returnString;
        }
    }
}