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
        private const int indentSize = 4;

        private readonly string Name;

        /// <summary>
        /// Construct a new Event instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the event.</param>
        public Event(IEventSymbol symbol)
        {
            this.Name = symbol.Name;
        }

        public string GetName()
        {
            return Name;
        }

        public string RenderEvent(int indents = 0)
        {
            string indent = new string(' ', indents * indentSize);

            //TODO: determine whether event is EventHandler or other type - and if it has type parameter(s)
            return indent + "public event EventHandler " + Name + ";";
        }

        public override string ToString()
        {
            //TODO: determine whether event is EventHandler or other type - and if it has type parameter(s)
            return "public event EventHandler " + Name + ";";
        }
    }
}