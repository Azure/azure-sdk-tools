using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
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
        private readonly string name;
        private readonly string type;

        private readonly bool constant;
        private readonly object value;

        /// <summary>
        /// Construct a new Field instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the field.</param>
        public Field(IFieldSymbol symbol)
        {
            this.name = symbol.Name;
            this.type = symbol.Type.ToDisplayString();
            this.constant = symbol.HasConstantValue;
            if (symbol.HasConstantValue)
                this.value = symbol.ConstantValue;
        }

        public string GetName()
        {
            return name;
        }

        public string GetFieldType()
        {
            return type;
        }

        public bool IsConstant()
        {
            return constant;
        }

        public object GetValue()
        {
            return value;
        }

        public override string ToString()
        {
            StringBuilder returnString = new StringBuilder("public");
            if (constant)
                returnString.Append(" const");
            returnString.Append(" " + type + " " + name);
            if (constant)
            {
                if (value.GetType().Name.Equals("String"))
                    returnString.Append(" = \"" + value + "\"");
                else
                    returnString.Append(" = " + value);
            }
            returnString.Append(";\n");
            return returnString.ToString();
        }
    }
}