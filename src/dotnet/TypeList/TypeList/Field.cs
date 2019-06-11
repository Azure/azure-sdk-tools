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
        private const int indentSize = 4;

        private readonly string Name;
        private readonly string Type;

        private readonly bool Constant;
        private readonly bool ReadOnly;
        private readonly bool Static;
        private readonly bool Volatile;
        
        private readonly object Value;

        /// <summary>
        /// Construct a new Field instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the field.</param>
        public Field(IFieldSymbol symbol)
        {
            this.Name = symbol.Name;
            this.Type = symbol.Type.ToDisplayString();

            this.Constant = symbol.HasConstantValue;
            this.ReadOnly = symbol.IsReadOnly;
            this.Static = symbol.IsStatic;
            this.Volatile = symbol.IsVolatile;

            if (symbol.HasConstantValue)
                this.Value = symbol.ConstantValue;
        }

        public string GetName()
        {
            return Name;
        }

        public string GetFieldType()
        {
            return Type;
        }

        public bool IsConstant()
        {
            return Constant;
        }

        public bool IsReadOnly()
        {
            return ReadOnly;
        }

        public bool IsStatic()
        {
            return Static;
        }

        public bool IsVolatile()
        {
            return Volatile;
        }

        public object GetValue()
        {
            return Value;
        }

        public string RenderField(int indents = 0)
        {
            string indent = new string(' ', indents * indentSize);

            StringBuilder returnString = new StringBuilder(indent + "public");

            if (Constant)
                returnString.Append(" const");

            if (Static)
                returnString.Append(" static");

            if (ReadOnly)
                returnString.Append(" readonly");

            if (Volatile)
                returnString.Append(" volatile");

            returnString.Append(" " + Type + " " + Name);

            if (Constant)
            {
                if (Value.GetType().Name.Equals("String"))
                    returnString.Append(" = \"" + Value + "\"");
                else
                    returnString.Append(" = " + Value);
            }

            returnString.Append(";");
            return returnString.ToString();
        }

        public override string ToString()
        {
            StringBuilder returnString = new StringBuilder("public");

            if (Constant)
                returnString.Append(" const");

            if (Static)
                returnString.Append(" static");

            if (ReadOnly)
                returnString.Append(" readonly");

            if (Volatile)
                returnString.Append(" volatile");

            returnString.Append(" " + Type + " " + Name);

            if (Constant)
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