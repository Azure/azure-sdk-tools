using Microsoft.CodeAnalysis;
using System.Text;

namespace APIView
{
    /// <summary>
    /// Class representing a field in a C# class/interface.
    /// 
    /// FieldAPIV is an immutable, thread-safe type.
    /// </summary>
    public class FieldAPIV
    {
        public string Name { get; }
        public string Type { get; }
        public string Accessibility { get; }

        public bool IsConstant { get; }
        public bool IsReadOnly { get; }
        public bool IsStatic { get; }
        public bool IsVolatile { get; }

        public object Value { get; }

        /// <summary>
        /// Construct a new FieldAPIV instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the field.</param>
        public FieldAPIV(IFieldSymbol symbol)
        {
            this.Name = symbol.Name;
            this.Type = symbol.Type.ToDisplayString();
            this.Accessibility = symbol.DeclaredAccessibility.ToString().ToLower();

            this.IsConstant = symbol.HasConstantValue;
            this.IsReadOnly = symbol.IsReadOnly;
            this.IsStatic = symbol.IsStatic;
            this.IsVolatile = symbol.IsVolatile;

            if (symbol.HasConstantValue)
                this.Value = symbol.ConstantValue;
        }

        public override string ToString()
        {
            var returnString = new StringBuilder();
            TreeRendererAPIV.RenderText(this, returnString);
            return returnString.ToString();
        }
    }
}
