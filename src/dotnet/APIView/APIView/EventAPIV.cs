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
        public string Name { get; }

        /// <summary>
        /// Construct a new EventAPIV instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the event.</param>
        public EventAPIV(IEventSymbol symbol)
        {
            this.Name = symbol.Name;
        }

        public override string ToString()
        {
            var returnString = new StringBuilder();
            TreeRendererAPIV.Render(this, returnString);
            return returnString.ToString();
        }
    }
}