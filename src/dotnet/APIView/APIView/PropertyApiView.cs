using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace ApiView
{
    /// <summary>
    /// Class representing a C# property.
    /// </summary>
    public class PropertyApiView
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public TypeReferenceApiView Type { get; set; }
        public string Accessibility { get; set; }

        public bool IsAbstract { get; set; }
        public bool IsVirtual { get; set; }
        public bool HasSetMethod { get; set; }

        public PropertyApiView() { }

        /// <summary>
        /// Construct a new PropertyApiView instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the property.</param>
        public PropertyApiView(IPropertySymbol symbol)
        {
            this.Id = symbol.ToDisplayString();
            this.Name = symbol.Name;
            this.Type = new TypeReferenceApiView(symbol.Type);
            this.Accessibility = symbol.DeclaredAccessibility.ToString().ToLower();

            this.IsAbstract = symbol.IsAbstract;
            this.IsVirtual = symbol.IsVirtual;
            this.HasSetMethod = (symbol.SetMethod != null) && 
                                (symbol.SetMethod.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public || 
                                 symbol.SetMethod.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Protected);
        }

        public override string ToString()
        {
            var renderer = new TextRendererApiView();
            var list = new StringListApiView();
            renderer.Render(this, list);
            return list.First().DisplayString;
        }
    }
}
