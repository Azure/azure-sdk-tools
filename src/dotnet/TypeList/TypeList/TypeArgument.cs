using Microsoft.CodeAnalysis;

namespace TypeList
{
    internal class TypeArgument
    {
        private readonly ITypeSymbol symbol;

        /// <summary>
        /// Construct a new TypeArgument instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the type argument.</param>
        public TypeArgument(ITypeSymbol symbol)
        {
            this.symbol = symbol;
        }

        public override string ToString()
        {
            return "Type argument: " + symbol + "\n";
        }
    }
}