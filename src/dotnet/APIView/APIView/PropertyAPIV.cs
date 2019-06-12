using Microsoft.CodeAnalysis;
using System.Text;

namespace APIView
{
    /// <summary>
    /// Class representing a C# property.
    /// 
    /// Property is an immutable, thread-safe type.
    /// </summary>
    public class PropertyAPIV
    {
        public string Name { get; }
        public string Type { get; }
        public bool HasSetMethod { get; }

        /// <summary>
        /// Construct a new Property instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the property.</param>
        public PropertyAPIV(IPropertySymbol symbol)
        {
            this.Name = symbol.Name;
            this.Type = symbol.Type.ToString();
            this.HasSetMethod = symbol.SetMethod != null;
        }

        public override string ToString()
        {
            StringBuilder returnString = new StringBuilder("public " + Type + " " + Name + " { get; ");
            if (HasSetMethod)
                returnString.Append("set; ");
            returnString.Append("}");
            return returnString.ToString();
        }
    }
}