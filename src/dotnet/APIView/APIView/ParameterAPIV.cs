using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Text;

namespace ApiView
{
    /// <summary>
    /// Class representing a C# method parameter.
    /// </summary>
    public class ParameterApiv
    {
        public string Name { get; set; }
        public bool HasExplicitDefaultValue { get; set; }
        public object ExplicitDefaultValue { get; set; }
        public TypeReferenceApiv Type { get; set; }
        public RefKind RefKind { get; set; }

        public string[] Attributes { get; set; }

        public ParameterApiv() { }

        /// <summary>
        /// Construct a new ParameterApiv instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the parameter.</param>
        public ParameterApiv(IParameterSymbol symbol)
        {
            this.Name = symbol.Name;
            this.Type = new TypeReferenceApiv(symbol.Type);
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
            var renderer = new TextRendererApiv();
            renderer.Render(this, returnString);
            return returnString.ToString();
        }
    }
}
