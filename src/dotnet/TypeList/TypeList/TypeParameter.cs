using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace TypeList
{
    internal class TypeParameter
    {
        private readonly ITypeParameterSymbol symbol;
        private readonly string name;
        private readonly ImmutableArray<AttributeData> attributes;

        /// <summary>
        /// Construct a new TypeParameter instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the type parameter.</param>
        public TypeParameter(ITypeParameterSymbol symbol)
        {
            this.symbol = symbol;
            this.name = symbol.ToString();
            this.attributes = symbol.GetAttributes();
        }

        public override string ToString()
        {
            return "Type parameter: " + symbol + "\n";
        }
    }
}