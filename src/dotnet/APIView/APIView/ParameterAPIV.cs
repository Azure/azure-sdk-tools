using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        public string Name { get; }
        public string Type { get; }

        public bool HasExplicitDefaultValue { get; }
        public object ExplicitDefaultValue { get; }

        public ImmutableArray<string> Attributes { get; }

        /// <summary>
        /// Construct a new ParameterAPIV instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the parameter.</param>
        public ParameterAPIV(IParameterSymbol symbol)
        {
            this.Name = symbol.Name;
            this.Type = symbol.ToString();

            this.HasExplicitDefaultValue = symbol.HasExplicitDefaultValue;
            this.ExplicitDefaultValue = HasExplicitDefaultValue ? symbol.ExplicitDefaultValue : null;

            List<string> attributes = new List<string>();
            foreach (AttributeData attribute in symbol.GetAttributes())
            {
                attributes.Add(attribute.ToString());
            }
            this.Attributes = attributes.ToImmutableArray();
        }

        public override string ToString()
        {
            var returnString = new StringBuilder();
            TreeRendererAPIV.RenderText(this, returnString);
            return returnString.ToString();
        }
    }
}
