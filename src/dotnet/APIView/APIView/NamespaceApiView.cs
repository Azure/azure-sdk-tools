using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace ApiView
{
    /// <summary>
    /// Class representing a C# namespace. Each namespace can contain named types 
    /// and/or other namespaces.
    /// </summary>
    public class NamespaceApiView
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public NamedTypeApiView[] NamedTypes { get; set; }
        public NamespaceApiView[] Namespaces { get; set; }

        public NamespaceApiView() { }

        /// <summary>
        /// Construct a new NamespaceApiView instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the namespace.</param>
        public NamespaceApiView(INamespaceSymbol symbol)
        {
            this.Name = symbol.ToDisplayString();
            this.Id = symbol.ToDisplayString();

            List<NamedTypeApiView> namedTypes = new List<NamedTypeApiView>();
            List<NamespaceApiView> namespaces = new List<NamespaceApiView>();

            foreach (var memberSymbol in symbol.GetMembers().OfType<INamespaceOrTypeSymbol>())
            {
                if (memberSymbol.DeclaredAccessibility != Accessibility.Public) continue;

                if (memberSymbol is INamedTypeSymbol nt) namedTypes.Add(new NamedTypeApiView(nt));

                else if (memberSymbol is INamespaceSymbol ns) namespaces.Add(new NamespaceApiView(ns));
            }

            this.NamedTypes = namedTypes.ToArray();
            this.Namespaces = namespaces.ToArray();
        }

        public override string ToString()
        {
            var renderer = new TextRendererApiView();
            var list = new StringListApiView();
            renderer.Render(this, list);
            return list.ToString();
        }
    }
}
