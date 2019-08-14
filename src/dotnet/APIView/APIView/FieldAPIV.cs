using Microsoft.CodeAnalysis;
using System.Linq;

namespace ApiView
{
    /// <summary>
    /// Class representing a field in a C# class/interface.
    /// </summary>
    public class FieldApiv
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public TypeReferenceApiv Type { get; set; }
        public string Accessibility { get; set; }

        public bool IsConstant { get; set; }
        public bool IsReadOnly { get; set; }
        public bool IsStatic { get; set; }
        public bool IsVolatile { get; set; }

        public object Value { get; set; }

        public FieldApiv() { }

        /// <summary>
        /// Construct a new FieldApiv instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the field.</param>
        public FieldApiv(IFieldSymbol symbol)
        {
            this.Id = symbol.ToDisplayString();
            this.Name = symbol.Name;
            this.Type = new TypeReferenceApiv(symbol.Type);
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
            var renderer = new TextRendererApiv();
            var list = new StringListApiv();
            renderer.Render(this, list);
            return list.First().DisplayString;
        }
    }
}
