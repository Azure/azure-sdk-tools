using Microsoft.CodeAnalysis;
using System;

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

        /// <summary>
        /// Construct a new Field instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the field.</param>
        public Field(IFieldSymbol symbol)
        {
            this.name = symbol.Name;
            this.type = symbol.Type.ToDisplayString();
        }

        public string GetName()
        {
            return name;
        }

        public string GetFieldType()
        {
            return type;
        }

        public override string ToString()
        {
            return "public " + type + " " + name + ";\n";
        }
    }
}