using Microsoft.CodeAnalysis;
using System.Text;

namespace APIView
{
    /// <summary>
    /// Class representing a C# event.
    /// 
    /// EventAPIV is an immutable, thread-safe type.
    /// </summary>
    public class EventAPIV
    {
        public string Name { get; set; }
        public string Accessibility { get; set; }

        public EventAPIV() { }

        /// <summary>
        /// Construct a new EventAPIV instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the event.</param>
        public EventAPIV(IEventSymbol symbol)
        {
            this.Name = symbol.Name;
            this.Accessibility = symbol.DeclaredAccessibility.ToString().ToLower();
        }

        public override string ToString()
        {
            var returnString = new StringBuilder();
            var renderer = new TextRendererAPIV();
            renderer.Render(this, returnString);
            return returnString.ToString();
        }
    }
}
