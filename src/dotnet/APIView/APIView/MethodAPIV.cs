using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Text;

namespace APIView
{
    /// <summary>
    /// Class representing a C# method. Each method includes a name, return type, attributes, 
    /// modifiers, type parameters, and parameters.
    /// </summary>
    public class MethodAPIV
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public TypeReferenceAPIV ReturnType { get; set; }
        public string Accessibility { get; set; }
        public string ClassNavigationID { get; set; }

        public bool IsConstructor { get; set; }
        public bool IsInterfaceMethod { get; set; }
        public bool IsStatic { get; set; }
        public bool IsVirtual { get; set; }
        public bool IsSealed { get; set; }
        public bool IsOverride { get; set; }
        public bool IsAbstract { get; set; }
        public bool IsExtern { get; set; }

        public AttributeAPIV[] Attributes { get; set; }
        public ParameterAPIV[] Parameters { get; set; }
        public TypeParameterAPIV[] TypeParameters { get; set; }

        public MethodAPIV() { }

        /// <summary>
        /// Construct a new MethodAPIV instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the method.</param>
        public MethodAPIV(IMethodSymbol symbol)
        {
            this.Id = symbol.ToDisplayString();
            this.IsConstructor = false;
            if (symbol.MethodKind == MethodKind.Constructor)
            {
                this.Name = symbol.ContainingType.Name;
                this.IsConstructor = true;
                this.ClassNavigationID = symbol.ContainingType.ToDisplayString();
            }
            else
            {
                this.Name = symbol.Name;
                this.ReturnType = new TypeReferenceAPIV(symbol.ReturnType);
                this.ClassNavigationID = "";
            }
            this.Accessibility = symbol.DeclaredAccessibility.ToString().ToLower();

            this.IsInterfaceMethod = symbol.ContainingType.TypeKind == TypeKind.Interface;
            this.IsStatic = symbol.IsStatic;
            this.IsVirtual = symbol.IsVirtual;
            this.IsSealed = symbol.IsSealed;
            this.IsOverride = symbol.IsOverride;
            this.IsAbstract = symbol.IsAbstract;
            this.IsExtern = symbol.IsExtern;

            List<AttributeAPIV> attributes = new List<AttributeAPIV>();
            List<TypeParameterAPIV> typeParameters = new List<TypeParameterAPIV>();
            List<ParameterAPIV> parameters = new List<ParameterAPIV>();

            foreach (AttributeData attribute in symbol.GetAttributes())
            {
                if (attribute.AttributeClass.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public || 
                    attribute.AttributeClass.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Protected)
                attributes.Add(new AttributeAPIV(attribute));
            }
            foreach (ITypeParameterSymbol typeParam in symbol.TypeParameters)
            {
                typeParameters.Add(new TypeParameterAPIV(typeParam));
            }
            foreach (IParameterSymbol param in symbol.Parameters)
            {
                parameters.Add(new ParameterAPIV(param));
            }

            this.Attributes = attributes.ToArray();
            this.TypeParameters = typeParameters.ToArray();
            this.Parameters = parameters.ToArray();
        }

        public override string ToString()
        {
            var renderer = new TextRendererAPIV();
            var list = new StringListAPIV();
            renderer.Render(this, list);
            return list.ToString();
        }
    }
}
