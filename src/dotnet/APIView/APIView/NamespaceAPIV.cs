using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace ApiView
{
    /// <summary>
    /// Class representing a C# namespace. Each namespace can contain named types 
    /// and/or other namespaces.
    /// </summary>
    public class NamespaceApiv
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public NamedTypeApiv[] NamedTypes { get; set; }
        public NamespaceApiv[] Namespaces { get; set; }

        public NamespaceApiv() { }

        /// <summary>
        /// Construct a new NamespaceApiv instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the namespace.</param>
        public NamespaceApiv(INamespaceSymbol symbol)
        {
            this.Name = symbol.ToDisplayString();
            this.Id = symbol.ToDisplayString();

            List<NamedTypeApiv> namedTypes = new List<NamedTypeApiv>();
            List<NamespaceApiv> namespaces = new List<NamespaceApiv>();

            foreach (var memberSymbol in symbol.GetMembers().OfType<INamespaceOrTypeSymbol>())
            {
                if (memberSymbol.DeclaredAccessibility != Accessibility.Public) continue;

                if (memberSymbol is INamedTypeSymbol nt) namedTypes.Add(new NamedTypeApiv(nt));

                else if (memberSymbol is INamespaceSymbol ns) namespaces.Add(new NamespaceApiv(ns));
            }

            this.NamedTypes = namedTypes.ToArray();
            this.Namespaces = namespaces.ToArray();
        }

        public override string ToString()
        {
            var renderer = new TextRendererApiv();
            var list = new StringListApiv();
            renderer.Render(this, list);
            return list.ToString();
        }
    }
}
