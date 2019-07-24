using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace APIView
{
    /// <summary>
    /// Class representing a C# property.
    /// 
    /// PropertyAPIV is an immutable, thread-safe type.
    /// </summary>
    public class PropertyAPIV
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public TypeReferenceAPIV Type { get; set; }
        public string Accessibility { get; set; }

        public bool IsAbstract { get; set; }
        public bool IsVirtual { get; set; }
        public bool HasSetMethod { get; set; }

        public PropertyAPIV() { }

        /// <summary>
        /// Construct a new PropertyAPIV instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the property.</param>
        public PropertyAPIV(IPropertySymbol symbol)
        {
            this.Id = symbol.ToDisplayString();
            this.Name = symbol.Name;
            this.Type = new TypeReferenceAPIV(symbol.Type);
            this.Accessibility = symbol.DeclaredAccessibility.ToString().ToLower();

            this.IsAbstract = symbol.IsAbstract;
            this.IsVirtual = symbol.IsVirtual;
            this.HasSetMethod = (symbol.SetMethod != null) && 
                                (symbol.SetMethod.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public || 
                                 symbol.SetMethod.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Protected);
        }

        public override string ToString()
        {
            var renderer = new TextRendererAPIV();
            var list = new StringListAPIV();
            renderer.Render(this, list);
            return list.First().DisplayString;
        }
    }
}
