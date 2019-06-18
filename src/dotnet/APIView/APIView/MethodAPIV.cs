using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace APIView
{
    /// <summary>
    /// Class representing a C# method. Each method includes a name, return type, attributes, 
    /// modifiers, type parameters, and parameters.
    /// 
    /// MethodAPIV is an immutable, thread-safe type.
    /// </summary>
    public class MethodAPIV
    {
        public string Name { get; }
        public string ReturnType { get; }
        public string Accessibility { get; }

        public bool IsInterfaceMethod { get; }
        public bool IsStatic { get; }
        public bool IsVirtual { get; }
        public bool IsSealed { get; }
        public bool IsOverride { get; }
        public bool IsAbstract { get; }
        public bool IsExtern { get; }

        public ImmutableArray<string> Attributes { get; }
        public ImmutableArray<ParameterAPIV> Parameters { get; }
        public ImmutableArray<TypeParameterAPIV> TypeParameters { get; }

        /// <summary>
        /// Construct a new MethodAPIV instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the method.</param>
        public MethodAPIV(IMethodSymbol symbol)
        {
            if (symbol.MethodKind == MethodKind.Constructor)
            {
                this.Name = symbol.ContainingType.Name;
                this.ReturnType = "";
            }
            else
            {
                this.Name = symbol.Name;
                this.ReturnType = symbol.ReturnType.ToString();
            }
            this.Accessibility = symbol.DeclaredAccessibility.ToString().ToLower();

            this.IsInterfaceMethod = symbol.ContainingType.TypeKind == TypeKind.Interface;
            this.IsStatic = symbol.IsStatic;
            this.IsVirtual = symbol.IsVirtual;
            this.IsSealed = symbol.IsSealed;
            this.IsOverride = symbol.IsOverride;
            this.IsAbstract = symbol.IsAbstract;
            this.IsExtern = symbol.IsExtern;

            List<string> attributes = new List<string>();
            List<TypeParameterAPIV> typeParameters = new List<TypeParameterAPIV>();
            List<ParameterAPIV> parameters = new List<ParameterAPIV>();

            foreach (AttributeData attribute in symbol.GetAttributes())
            {
                attributes.Add(attribute.ToString());
            }
            foreach (ITypeParameterSymbol typeParam in symbol.TypeParameters)
            {
                typeParameters.Add(new TypeParameterAPIV(typeParam));
            }
            foreach (IParameterSymbol param in symbol.Parameters)
            {
                parameters.Add(new ParameterAPIV(param));
            }

            this.Attributes = attributes.ToImmutableArray();
            this.TypeParameters = typeParameters.ToImmutableArray();
            this.Parameters = parameters.ToImmutableArray();
        }

        public override string ToString()
        {
            var returnString = new StringBuilder();
            TreeRendererAPIV.RenderText(this, returnString);
            return returnString.ToString();
        }
    }
}
