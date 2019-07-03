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
        public string Name { get; set; }
        public string Type { get; set; }
        public string Accessibility { get; set; }

        public bool IsConstant { get; set; }
        public bool IsReadOnly { get; set; }
        public bool IsStatic { get; set; }
        public bool IsVolatile { get; set; }

        public object Value { get; set; }

        public FieldAPIV() { }

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
            var renderer = new TextRendererAPIV();
            renderer.Render(this, returnString);
            return returnString.ToString();
        }
    }
}
