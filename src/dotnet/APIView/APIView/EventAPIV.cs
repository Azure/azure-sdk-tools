using Microsoft.CodeAnalysis;
using System.Linq;

namespace ApiView
{
    /// <summary>
    /// Class representing a C# event.
    /// </summary>
    public class EventApiv
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Accessibility { get; set; }
        public TypeReferenceApiv Type { get; set; }

        public EventApiv() { }

        /// <summary>
        /// Construct a new EventApiv instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the event.</param>
        public EventApiv(IEventSymbol symbol)
        {
            this.Id = symbol.ToDisplayString();
            this.Name = symbol.Name;
            this.Accessibility = symbol.DeclaredAccessibility.ToString().ToLower();
            this.Type = new TypeReferenceApiv(symbol.Type);
        }

        public override string ToString()
        {
            var renderer = new TextRendererApiv();
            var list = new StringListApiv();
            renderer.Render(this, list);
            return list.First().DisplayString;
        }
    }
}
