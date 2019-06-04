using Microsoft.CodeAnalysis;

namespace TypeList
{
    internal class Event
    {
        private readonly IEventSymbol symbol;

        /// <summary>
        /// Construct a new Event instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the event.</param>
        public Event(IEventSymbol symbol)
        {
            this.symbol = symbol;
        }

        public override string ToString()
        {
            return "Event: " + symbol + "\n";
        }
    }
}