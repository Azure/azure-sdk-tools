using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        public string Name { get; }
        public ImmutableArray<string> Attributes { get; }

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
            this.Attributes = attributes.ToImmutableArray();
        }

        public override string ToString()
        {
            var returnString = new StringBuilder();
            TreeRendererAPIV.RenderText(this, returnString);
            return returnString.ToString();
        }
    }
}
