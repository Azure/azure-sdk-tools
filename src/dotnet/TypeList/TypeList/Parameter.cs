using Microsoft.CodeAnalysis;
using System.Text;

namespace TypeList
{
    public class Parameter
    {
        private readonly string Name;
        private readonly string Type;

        private readonly bool HasExplicitDefaultValue;
        private readonly object ExplicitDefaultValue = null;

        /// <summary>
        /// Construct a new Parameter instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the parameter.</param>
        public Parameter(IParameterSymbol symbol)
        {
            this.Name = symbol.Name;
            this.Type = symbol.ToString();

            this.HasExplicitDefaultValue = symbol.HasExplicitDefaultValue;
            if (symbol.HasExplicitDefaultValue)
                this.ExplicitDefaultValue = symbol.ExplicitDefaultValue;
        }

        public string GetName()
        {
            return Name;
        }

        public string GetParameterType()
        {
            return Type;
        }

        public object GetExplicitDefaultValue()
        {
            return ExplicitDefaultValue;
        }

        public string RenderParameter()
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