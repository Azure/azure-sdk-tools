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
        private const int INDENT_SIZE = 4;

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

        public string RenderEvent(int indents = 0)
        {
            string indent = new string(' ', indents * INDENT_SIZE);

            //TODO: determine whether event is EventHandler or other type - and if it has type parameter(s)
            return indent + "public event EventHandler " + name + ";";
        }

        public override string ToString()
        {
            //TODO: determine whether event is EventHandler or other type - and if it has type parameter(s)
            return "public event EventHandler " + name + ";";
        }
    }
}