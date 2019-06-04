using Microsoft.CodeAnalysis;

namespace TypeList
{
    internal class Assembly
    {
        private readonly IAssemblySymbol symbol;

        private Namespace globalNamespace;

        /// <summary>
        /// Construct a new Assembly instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the assembly.</param>
        public Assembly(IAssemblySymbol symbol)
        {
            this.symbol = symbol;
            this.globalNamespace = new Namespace(symbol.GlobalNamespace);
        }

        public override string ToString()
        {
            return "Assembly: " + symbol + "\n" + globalNamespace.ToString();
        }
    }
}
