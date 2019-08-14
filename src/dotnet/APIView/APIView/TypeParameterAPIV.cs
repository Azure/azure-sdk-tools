using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Text;

namespace ApiView
{
    /// <summary>
    /// Class representing a C# type parameter.
    /// </summary>
    public class TypeParameterApiv
    {
        public string Name { get; set; }
        public string[] Attributes { get; set; }

        public TypeParameterApiv() { }

        /// <summary>
        /// Construct a new TypeParameterApiv instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the type parameter.</param>
        public TypeParameterApiv(ITypeParameterSymbol symbol)
        {
            this.Name = symbol.ToString();

            List<string> attributes = new List<string>();
            foreach (AttributeData attribute in symbol.GetAttributes())
            {
                attributes.Add(attribute.ToString());
            }
            this.Attributes = attributes.ToArray();
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            var renderer = new TextRendererApiv();
            renderer.Render(this, builder);
            return builder.ToString();
        }
    }
}
