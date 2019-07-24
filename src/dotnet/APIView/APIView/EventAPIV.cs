using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace APIView
{
    /// <summary>
    /// Class representing a C# event.
    /// 
    /// EventAPIV is an immutable, thread-safe type.
    /// </summary>
    public class EventAPIV
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Accessibility { get; set; }
        public TypeReferenceAPIV Type { get; set; }

        public EventAPIV() { }

        /// <summary>
        /// Construct a new EventAPIV instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the event.</param>
        public EventAPIV(IEventSymbol symbol)
        {
            this.Id = symbol.ToDisplayString();
            this.Name = symbol.Name;
            this.Accessibility = symbol.DeclaredAccessibility.ToString().ToLower();
            this.Type = new TypeReferenceAPIV(symbol.Type);
        }

        public override string ToString()
        {
            var renderer = new TextRendererAPIV();
            var list = new StringListAPIV();
            renderer.Render(this, list);
            return list.First().DisplayString;
        }
    }
}
