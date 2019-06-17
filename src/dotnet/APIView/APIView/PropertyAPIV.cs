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
        public string Name { get; }
        public string Type { get; }
        public bool IsAbstract { get; }
        public bool IsVirtual { get; }
        public bool HasSetMethod { get; }

        /// <summary>
        /// Construct a new PropertyAPIV instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the property.</param>
        public PropertyAPIV(IPropertySymbol symbol)
        {
            this.Name = symbol.Name;
            this.Type = symbol.Type.ToString();

            this.IsAbstract = symbol.IsAbstract;
            this.IsVirtual = symbol.IsVirtual;
            this.HasSetMethod = (symbol.SetMethod != null) && (symbol.SetMethod.DeclaredAccessibility == Accessibility.Public);
        }

        public override string ToString()
        {
            var returnString = new StringBuilder();
            TreeRendererAPIV.RenderText(this, returnString);
            return returnString.ToString();
        }
    }
}
