using Microsoft.CodeAnalysis;

namespace TypeList
{
    internal class TypeParameter
    {
        private readonly ITypeParameterSymbol symbol;

        /// <summary>
        /// Construct a new TypeParameter instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the type parameter.</param>
        public TypeParameter(ITypeParameterSymbol symbol)
        {
            this.symbol = symbol;
        }

        public override string ToString()
        {
            return "Type parameter: " + symbol + "\n";
        }
    }
}