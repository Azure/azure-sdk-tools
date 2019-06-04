using Microsoft.CodeAnalysis;

namespace TypeList
{
    internal class Field
    {
        private readonly IFieldSymbol symbol;

        /// <summary>
        /// Construct a new Field instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the field.</param>
        public Field(IFieldSymbol symbol)
        {
            this.symbol = symbol;
        }

        public override string ToString()
        {
            return "Field: " + symbol + "\n";
        }
    }
}