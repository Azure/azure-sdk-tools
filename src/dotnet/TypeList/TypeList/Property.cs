using Microsoft.CodeAnalysis;
using System.Text;

namespace TypeList
{
    /// <summary>
    /// Class representing a C# property.
    /// 
    /// Property is an immutable, thread-safe type.
    /// </summary>
    public class Property
    {
        private const int INDENT_SIZE = 4;

        private readonly string name;
        private readonly string type;
        private readonly bool hasSet;

        /// <summary>
        /// Construct a new Property instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the property.</param>
        public Property(IPropertySymbol symbol)
        {
            this.name = symbol.Name;
            this.type = symbol.Type.ToString();
            this.hasSet = symbol.SetMethod != null;
        }

        public string GetName()
        {
            return name;
        }

        public string GetPropertyType()
        {
            return type;
        }

        public bool HasSetMethod()
        {
            return hasSet;
        }

        public string RenderProperty(int indents = 0)
        {
            string indent = new string(' ', indents * INDENT_SIZE);

            StringBuilder returnString = new StringBuilder(indent + "public " + type + " " + name + " { get; ");
            if (hasSet)
                returnString.Append("set; ");
            returnString.Append("}");
            return returnString.ToString();
        }

        public override string ToString()
        {
            StringBuilder returnString = new StringBuilder("public " + type + " " + name + " { get; ");
            if (hasSet)
                returnString.Append("set; ");
            returnString.Append("}");
            return returnString.ToString();
        }
    }
}