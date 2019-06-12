using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace APIView
{
    public class Namespace
    {
        public string Name { get; }

        public ImmutableArray<NamedType> NamedTypes { get; }
        public ImmutableArray<Namespace> Namespaces { get; }

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