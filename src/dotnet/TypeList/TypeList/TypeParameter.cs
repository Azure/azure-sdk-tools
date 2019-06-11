using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Text;

namespace TypeList
{
    public class TypeParameter
    {
        private readonly string Name;
        private readonly ImmutableArray<AttributeData> Attributes;

        /// <summary>
        /// Construct a new TypeParameter instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the type parameter.</param>
        public TypeParameter(ITypeParameterSymbol symbol)
        {
            this.Name = symbol.ToString();
            this.Attributes = symbol.GetAttributes();
        }

        public string GetName()
        {
            return Name;
        }

        public ImmutableArray<AttributeData> GetAttributes()
        {
            return Attributes;
        }

        public string RenderTypeParameter()
        {
            StringBuilder returnString = new StringBuilder("");
            if (Attributes.Length != 0)
                returnString.Append(Attributes + " ");
            returnString.Append(Name);
            return returnString.ToString();
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