using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Text;

namespace APIView
{
    public class TypeParameterAPIV
    {
        public string Name { get; }
        public ImmutableArray<AttributeData> Attributes { get; }

        /// <summary>
        /// Construct a new TypeParameter instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the type parameter.</param>
        public TypeParameterAPIV(ITypeParameterSymbol symbol)
        {
            this.Name = symbol.ToString();
            this.Attributes = symbol.GetAttributes();
        }

        public override string ToString()
        {
            var returnString = new StringBuilder();
            TreeRendererAPIV.Render(this, returnString);
            return returnString.ToString();
        }
    }
}