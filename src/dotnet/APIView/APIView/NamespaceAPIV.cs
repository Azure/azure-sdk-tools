using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace APIView
{
    public class NamespaceAPIV
    {
        public string Name { get; }

        public ImmutableArray<NamedTypeAPIV> NamedTypes { get; }
        public ImmutableArray<NamespaceAPIV> Namespaces { get; }

        /// <summary>
        /// Construct a new namespace instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the namespace.</param>
        public NamespaceAPIV(INamespaceSymbol symbol)
        {
            this.Name = symbol.Name;

            List<NamedTypeAPIV> namedTypes = new List<NamedTypeAPIV>();
            List<NamespaceAPIV> namespaces = new List<NamespaceAPIV>();

            foreach (var memberSymbol in symbol.GetMembers().OfType<INamespaceOrTypeSymbol>())
            {
                if (memberSymbol.DeclaredAccessibility != Accessibility.Public) continue;

                if (memberSymbol is INamedTypeSymbol nt) namedTypes.Add(new NamedTypeAPIV(nt));

                else if (memberSymbol is INamespaceSymbol ns) namespaces.Add(new NamespaceAPIV(ns));
            }

            this.NamedTypes = namedTypes.ToImmutableArray();
            this.Namespaces = namespaces.ToImmutableArray();
        }

        public override string ToString()
        {
            StringBuilder returnString = new StringBuilder("");

            if (Name.Length != 0)
                returnString = new StringBuilder("namespace " + Name + " {\n");

            foreach (NamedTypeAPIV nt in NamedTypes)
            {
                returnString.Append(nt.ToString() + "\n");
            }
            foreach(NamespaceAPIV ns in Namespaces)
            {
                returnString.Append(ns.ToString() + "\n");
            }

            if (Name.Length != 0)
                returnString.Append("}");

            return returnString.ToString();
        }
    }
}