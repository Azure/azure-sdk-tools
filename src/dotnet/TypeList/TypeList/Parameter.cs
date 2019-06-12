using Microsoft.CodeAnalysis;
using System.Text;

namespace TypeList
{
    public class Parameter
    {
        public string Name { get; }
        public string Type { get; }

        public bool HasExplicitDefaultValue { get; }
        public object ExplicitDefaultValue { get; }

        /// <summary>
        /// Construct a new Parameter instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the parameter.</param>
        public Parameter(IParameterSymbol symbol)
        {
            this.Name = symbol.Name;
            this.Type = symbol.ToString();

            this.HasExplicitDefaultValue = symbol.HasExplicitDefaultValue;
            this.ExplicitDefaultValue = HasExplicitDefaultValue ? symbol.ExplicitDefaultValue : null;
        }

        public override string ToString()
        {
            StringBuilder returnString = new StringBuilder(Type + " " + Name);
            if (HasExplicitDefaultValue)
            {
                if (Type.Equals("string"))
                    returnString.Append(" = \"" + ExplicitDefaultValue + "\"");
                else
                    returnString.Append(" = " + ExplicitDefaultValue);
            }
            return returnString.ToString();
        }
    }
}