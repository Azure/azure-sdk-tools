using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Text;

namespace ApiView
{
    /// <summary>
    /// Class representing a C# method. Each method includes a name, return type, attributes, 
    /// modifiers, type parameters, and parameters.
    /// </summary>
    public class MethodApiv
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public TypeReferenceApiv ReturnType { get; set; }
        public string Accessibility { get; set; }

        public bool IsConstructor { get; set; }
        public bool IsInterfaceMethod { get; set; }
        public bool IsStatic { get; set; }
        public bool IsVirtual { get; set; }
        public bool IsSealed { get; set; }
        public bool IsOverride { get; set; }
        public bool IsAbstract { get; set; }
        public bool IsExtern { get; set; }

        public AttributeApiv[] Attributes { get; set; }
        public ParameterApiv[] Parameters { get; set; }
        public TypeParameterApiv[] TypeParameters { get; set; }

        public MethodApiv() { }

        /// <summary>
        /// Construct a new MethodAPIV instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the method.</param>
        public MethodApiv(IMethodSymbol symbol)
        {
            this.Id = symbol.ToDisplayString();
            this.IsConstructor = false;
            if (symbol.MethodKind == MethodKind.Constructor)
            {
                this.Name = symbol.ContainingType.Name;
                this.IsConstructor = true;
            }
            else
            {
                this.Name = symbol.Name;
                this.ReturnType = new TypeReferenceApiv(symbol.ReturnType);
            }
            this.Accessibility = symbol.DeclaredAccessibility.ToString().ToLower();

            this.IsInterfaceMethod = symbol.ContainingType.TypeKind == TypeKind.Interface;
            this.IsStatic = symbol.IsStatic;
            this.IsVirtual = symbol.IsVirtual;
            this.IsSealed = symbol.IsSealed;
            this.IsOverride = symbol.IsOverride;
            this.IsAbstract = symbol.IsAbstract;
            this.IsExtern = symbol.IsExtern;

            List<AttributeApiv> attributes = new List<AttributeApiv>();
            List<TypeParameterApiv> typeParameters = new List<TypeParameterApiv>();
            List<ParameterApiv> parameters = new List<ParameterApiv>();

            foreach (AttributeData attribute in symbol.GetAttributes())
            {
                if (attribute.AttributeClass.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public || 
                    attribute.AttributeClass.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Protected)
                attributes.Add(new AttributeApiv(attribute, this.Id));
            }
            foreach (ITypeParameterSymbol typeParam in symbol.TypeParameters)
            {
                typeParameters.Add(new TypeParameterApiv(typeParam));
            }
            foreach (IParameterSymbol param in symbol.Parameters)
            {
                parameters.Add(new ParameterApiv(param));
            }

            this.Attributes = attributes.ToArray();
            this.TypeParameters = typeParameters.ToArray();
            this.Parameters = parameters.ToArray();
        }

        public override string ToString()
        {
            var renderer = new TextRendererApiv();
            var list = new StringListApiv();
            renderer.Render(this, list);
            return list.ToString();
        }
    }
}
