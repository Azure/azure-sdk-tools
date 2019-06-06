using Microsoft.CodeAnalysis;

namespace TypeList
{
    public class Parameter
    {
        private readonly string name;
        private readonly string type;
        private readonly string refKind = RefKind.None.ToString();
        private readonly object explicitDefaultValue = null;

        /// <summary>
        /// Construct a new Parameter instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the parameter.</param>
        public Parameter(IParameterSymbol symbol)
        {
            this.name = symbol.Name;
            this.type = symbol.ToString();
            this.refKind = symbol.RefKind.ToString();
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

        public string GetRefKind()
        {
            return refKind;
        }

        public object GetExplicitDefaultValue()
        {
            return explicitDefaultValue;
        }

        public override string ToString()
        {
            string returnString = "";
            if (refKind != RefKind.None.ToString())
                returnString += refKind + " ";
            returnString += type + " " + name;
            if (explicitDefaultValue != null)
                returnString += " = " + explicitDefaultValue;
            return returnString;
        }
    }
}