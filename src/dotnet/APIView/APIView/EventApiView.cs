using Microsoft.CodeAnalysis;
using System.Linq;

namespace ApiView
{
    /// <summary>
    /// Class representing a C# event.
    /// </summary>
    public class EventApiView
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Accessibility { get; set; }
        public TypeReferenceApiView Type { get; set; }

        public EventApiView() { }

        /// <summary>
        /// Construct a new EventApiView instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the event.</param>
        public EventApiView(IEventSymbol symbol)
        {
            this.Id = symbol.ToDisplayString();
            this.Name = symbol.Name;
            this.Accessibility = symbol.DeclaredAccessibility.ToString().ToLower();
            this.Type = new TypeReferenceApiView(symbol.Type);
        }

        public override string ToString()
        {
            var renderer = new TextRendererApiView();
            var list = new StringListApiView();
            renderer.Render(this, list);
            return list.First().DisplayString;
        }
    }
}
