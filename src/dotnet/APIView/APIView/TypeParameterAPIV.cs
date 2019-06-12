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
            StringBuilder returnString = new StringBuilder("");
            if (Attributes.Length != 0)
                returnString.Append(Attributes + " ");
            returnString.Append(Name);
            return returnString.ToString();
        }
    }
}