using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Text;

namespace ApiView
{
    /// <summary>
    /// Class representing a C# method parameter.
    /// </summary>
    public class ParameterApiView
    {
        public string Name { get; set; }
        public bool HasExplicitDefaultValue { get; set; }
        public object ExplicitDefaultValue { get; set; }
        public TypeReferenceApiView Type { get; set; }
        public RefKind RefKind { get; set; }

        public string[] Attributes { get; set; }

        public ParameterApiView() { }

        /// <summary>
        /// Construct a new ParameterApiView instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the parameter.</param>
        public ParameterApiView(IParameterSymbol symbol)
        {
            this.Name = symbol.Name;
            this.Type = new TypeReferenceApiView(symbol.Type);
            this.RefKind = symbol.RefKind;

            this.HasExplicitDefaultValue = symbol.HasExplicitDefaultValue;
            this.ExplicitDefaultValue = HasExplicitDefaultValue ? symbol.ExplicitDefaultValue : null;

            List<string> attributes = new List<string>();
            foreach (AttributeData attribute in symbol.GetAttributes())
            {
                attributes.Add(attribute.ToString());
            }
            this.Attributes = attributes.ToArray();
        }

        public override string ToString()
        {
            var returnString = new StringBuilder();
            var renderer = new TextRendererApiView();
            renderer.Render(this, returnString);
            return returnString.ToString();
        }
    }
}
