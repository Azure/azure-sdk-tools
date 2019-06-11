using Microsoft.CodeAnalysis;
using System.Collections;
using System.Text;

namespace TypeList
{
    public class Parameter
    {
        private readonly string name;
        private readonly string type;

        private readonly bool hasExplicitDefaultValue;
        private readonly object explicitDefaultValue;

        /// <summary>
        /// Construct a new Parameter instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the parameter.</param>
        public Parameter(IParameterSymbol symbol)
        {
            this.name = symbol.Name;
            this.type = symbol.ToString();

            this.hasExplicitDefaultValue = symbol.HasExplicitDefaultValue;
            if (symbol.HasExplicitDefaultValue)
                this.explicitDefaultValue = symbol.ExplicitDefaultValue;
        }

        public string GetName()
        {
            return name;
        }

        public string GetParameterType()
        {
            return type;
        }

        public bool HasExplicitDefaultValue()
        {
            return hasExplicitDefaultValue;
        }

        public object GetExplicitDefaultValue()
        {
            return explicitDefaultValue;
        }

        public string RenderParameter()
        {
            StringBuilder returnString = new StringBuilder(type + " " + name);
            if (hasExplicitDefaultValue)
            {
                if (type.Equals("string"))
                    returnString.Append(" = \"" + explicitDefaultValue + "\"");
                else
                    returnString.Append(" = " + explicitDefaultValue);
            }
            return returnString.ToString();
        }

        public override string ToString()
        {
            StringBuilder returnString = new StringBuilder(type + " " + name);
            if (hasExplicitDefaultValue)
            {
                if (type.Equals("string"))
                    returnString.Append(" = \"" + explicitDefaultValue + "\"");
                else
                    returnString.Append(" = " + explicitDefaultValue);
            }
            return returnString.ToString();
        }
    }
}