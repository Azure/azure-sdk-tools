using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Text;

namespace TypeList
{
    public class TypeParameter
    {
        private readonly string name;
        private readonly ImmutableArray<AttributeData> attributes;

        /// <summary>
        /// Construct a new TypeParameter instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the type parameter.</param>
        public TypeParameter(ITypeParameterSymbol symbol)
        {
            this.name = symbol.ToString();
            this.attributes = symbol.GetAttributes();
        }

        public string GetName()
        {
            return name;
        }

        public ImmutableArray<AttributeData> GetAttributes()
        {
            return attributes;
        }

        public override string ToString()
        {
            StringBuilder returnString = new StringBuilder("");
            if (attributes.Length != 0)
                returnString.Append(attributes + " ");
            returnString.Append(name);
            return returnString.ToString();
        }
    }
}