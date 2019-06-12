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
        public string Name { get; }

        /// <summary>
        /// Construct a new Event instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the event.</param>
        public Event(IEventSymbol symbol)
        {
            this.Name = symbol.Name;
        }

        public override string ToString()
        {
            //TODO: determine whether event is EventHandler or other type - and if it has type parameter(s)
            return "public event EventHandler " + Name + ";";
        }
    }
}