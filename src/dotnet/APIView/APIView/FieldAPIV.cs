using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace APIView
{
    /// <summary>
    /// Class representing a field in a C# class/interface.
    /// 
    /// FieldAPIV is an immutable, thread-safe type.
    /// </summary>
    public class FieldAPIV
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public TypeReferenceAPIV Type { get; set; }
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
            this.Id = symbol.ToDisplayString();
            this.Name = symbol.Name;
            this.Type = new TypeReferenceAPIV(symbol.Type);
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
            var renderer = new TextRendererAPIV();
            var list = new StringListAPIV();
            renderer.Render(this, list);
            return list.First().DisplayString;
        }
    }
}
