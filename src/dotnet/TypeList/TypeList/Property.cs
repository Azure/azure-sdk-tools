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
        private const int indentSize = 4;

        private readonly string Name;
        private readonly string Type;
        private readonly bool HasSet;

        /// <summary>
        /// Construct a new Property instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the property.</param>
        public Property(IPropertySymbol symbol)
        {
            this.Name = symbol.Name;
            this.Type = symbol.Type.ToString();
            this.HasSet = symbol.SetMethod != null;
        }

        public string GetName()
        {
            return Name;
        }

        public string GetPropertyType()
        {
            return Type;
        }

        public bool HasSetMethod()
        {
            return HasSet;
        }

        public string RenderProperty(int indents = 0)
        {
            string indent = new string(' ', indents * indentSize);

            StringBuilder returnString = new StringBuilder(indent + "public " + Type + " " + Name + " { get; ");
            if (HasSet)
                returnString.Append("set; ");
            returnString.Append("}");
            return returnString.ToString();
        }

        public override string ToString()
        {
            StringBuilder returnString = new StringBuilder("public " + Type + " " + Name + " { get; ");
            if (HasSet)
                returnString.Append("set; ");
            returnString.Append("}");
            return returnString.ToString();
        }
    }
}