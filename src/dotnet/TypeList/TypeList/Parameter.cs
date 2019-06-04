using Microsoft.CodeAnalysis;

namespace TypeList
{
    internal class Parameter
    {
        private readonly IParameterSymbol symbol;

        /// <summary>
        /// Construct a new Parameter instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the parameter.</param>
        public Parameter(IParameterSymbol symbol)
        {
            this.symbol = symbol;
        }

        public override string ToString()
        {
            return "Parameter: " + symbol + "\n";
        }
    }
}