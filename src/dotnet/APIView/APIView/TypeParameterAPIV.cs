using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Text;

namespace APIView
{
    /// <summary>
    /// Class representing a C# type parameter.
    /// 
    /// TypeParameterAPIV is an immutable, thread-safe type.
    /// </summary>
    public class TypeParameterAPIV
    {
        public string Name { get; set; }
        public string[] Attributes { get; set; }

        public TypeParameterAPIV() { }

        /// <summary>
        /// Construct a new TypeParameterAPIV instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the type parameter.</param>
        public TypeParameterAPIV(ITypeParameterSymbol symbol)
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
            var renderer = new TextRendererAPIV();
            renderer.Render(this, builder);
            return builder.ToString();
        }
    }
}
