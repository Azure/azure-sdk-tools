using Microsoft.CodeAnalysis;
using System.Text;

namespace TypeList
{
    /// <summary>
    /// Class representing a field in a C# class/interface.
    /// 
    /// Field is an immutable, thread-safe type.
    /// </summary>
    public class Field
    {
        public string Name { get; }
        public string Type { get; }

        public bool IsConstant { get; }
        public bool IsReadOnly { get; }
        public bool IsStatic { get; }
        public bool IsVolatile { get; }

        public object Value { get; }

        /// <summary>
        /// Construct a new Field instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the field.</param>
        public Field(IFieldSymbol symbol)
        {
            this.Name = symbol.Name;
            this.Type = symbol.Type.ToDisplayString();

            this.IsConstant = symbol.HasConstantValue;
            this.IsReadOnly = symbol.IsReadOnly;
            this.IsStatic = symbol.IsStatic;
            this.IsVolatile = symbol.IsVolatile;

            if (symbol.HasConstantValue)
                this.Value = symbol.ConstantValue;
        }

        public override string ToString()
        {
            StringBuilder returnString = new StringBuilder("public");

            if (IsConstant)
                returnString.Append(" const");

            if (IsStatic)
                returnString.Append(" static");

            if (IsReadOnly)
                returnString.Append(" readonly");

            if (IsVolatile)
                returnString.Append(" volatile");

            returnString.Append(" " + Type + " " + Name);

            if (IsConstant)
            {
                if (Value.GetType().Name.Equals("String"))
                    returnString.Append(" = \"" + Value + "\"");
                else
                    returnString.Append(" = " + Value);
            }

            returnString.Append(";");
            return returnString.ToString();
        }
    }
}