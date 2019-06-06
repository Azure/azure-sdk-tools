using Microsoft.CodeAnalysis;

namespace TypeList
{
    /// <summary>
    /// Class representing a C# event.
    /// 
    /// Event is an immutable, thread-safe type.
    /// </summary>
    public class Event
    {
        private readonly string name;

        /// <summary>
        /// Construct a new Event instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the event.</param>
        public Event(IEventSymbol symbol)
        {
            this.name = symbol.Name;
        }

        public string GetName()
        {
            return name;
        }

        public override string ToString()
        {
            return "Event: " + name + "\n";
        }
    }
}