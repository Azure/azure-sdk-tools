using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace APIView
{
    /// <summary>
    /// Class representing a C# method parameter.
    /// 
    /// ParameterAPIV is an immutable, thread-safe type.
    /// </summary>
    public class ParameterAPIV
    {
        public string Name { get; set; }
        public Token[] TypeParts { get; set; }

        public bool HasExplicitDefaultValue { get; set; }
        public object ExplicitDefaultValue { get; set; }

        public string[] Attributes { get; set; }

        public ParameterAPIV() { }

        /// <summary>
        /// Construct a new ParameterAPIV instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the parameter.</param>
        public ParameterAPIV(IParameterSymbol symbol)
        {
            this.Name = symbol.Name;

            var type = new List<Token>();
            foreach (var part in symbol.ToDisplayParts())
            {
                var token = new Token();
                token.DisplayString = part.ToString();
                token.Type = part.Kind == SymbolDisplayPartKind.Keyword ? TypeReference.BuiltInType : TypeReference.SpecialType;
                type.Add(token);
            }
            this.TypeParts = type.ToArray();

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
            var renderer = new TextRendererAPIV();
            renderer.Render(this, returnString);
            return returnString.ToString();
        }
    }
}
