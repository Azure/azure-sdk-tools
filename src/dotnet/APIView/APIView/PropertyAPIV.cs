using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace ApiView
{
    /// <summary>
    /// Class representing a C# property.
    /// </summary>
    public class PropertyApiv
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public TypeReferenceApiv Type { get; set; }
        public string Accessibility { get; set; }

        public bool IsAbstract { get; set; }
        public bool IsVirtual { get; set; }
        public bool HasSetMethod { get; set; }

        public PropertyApiv() { }

        /// <summary>
        /// Construct a new PropertyApiv instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the property.</param>
        public PropertyApiv(IPropertySymbol symbol)
        {
            this.Id = symbol.ToDisplayString();
            this.Name = symbol.Name;
            this.Type = new TypeReferenceApiv(symbol.Type);
            this.Accessibility = symbol.DeclaredAccessibility.ToString().ToLower();

            this.IsAbstract = symbol.IsAbstract;
            this.IsVirtual = symbol.IsVirtual;
            this.HasSetMethod = (symbol.SetMethod != null) && 
                                (symbol.SetMethod.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public || 
                                 symbol.SetMethod.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Protected);
        }

        public override string ToString()
        {
            var renderer = new TextRendererApiv();
            var list = new StringListApiv();
            renderer.Render(this, list);
            return list.First().DisplayString;
        }
    }
}
