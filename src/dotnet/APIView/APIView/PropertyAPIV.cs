using Microsoft.CodeAnalysis;
using System.Text;

namespace APIView
{
    /// <summary>
    /// Class representing a C# property.
    /// 
    /// PropertyAPIV is an immutable, thread-safe type.
    /// </summary>
    public class PropertyAPIV
    {
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
            var returnString = new StringBuilder();
            var renderer = new TextRendererAPIV();
            renderer.Render(this, returnString);
            return returnString.ToString();
        }
    }
}
