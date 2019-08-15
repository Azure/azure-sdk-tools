using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace ApiView
{
    /// <summary>
    /// Class representing a C# method. Each method includes a name, return type, attributes, 
    /// modifiers, type parameters, and parameters.
    /// </summary>
    public class MethodApiView
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public TypeReferenceApiView ReturnType { get; set; }
        public string Accessibility { get; set; }

        public bool IsConstructor { get; set; }
        public bool IsInterfaceMethod { get; set; }
        public bool IsStatic { get; set; }
        public bool IsVirtual { get; set; }
        public bool IsSealed { get; set; }
        public bool IsOverride { get; set; }
        public bool IsAbstract { get; set; }
        public bool IsExtensionMethod { get; set; }
        public bool IsExtern { get; set; }

        public AttributeApiView[] Attributes { get; set; }
        public ParameterApiView[] Parameters { get; set; }
        public TypeParameterApiView[] TypeParameters { get; set; }

        static readonly List<string> ignoredAttributeNames = new List<string>() { "AsyncStateMachineAttribute", "DebuggerStepThroughAttribute" };

        public MethodApiView() { }

        /// <summary>
        /// Construct a new MethodAPIV instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the method.</param>
        public MethodApiView(IMethodSymbol symbol)
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
                this.ReturnType = new TypeReferenceApiView(symbol.ReturnType);
            }
            this.Accessibility = symbol.DeclaredAccessibility.ToString().ToLower();

            this.IsInterfaceMethod = symbol.ContainingType.TypeKind == TypeKind.Interface;
            this.IsStatic = symbol.IsStatic;
            this.IsVirtual = symbol.IsVirtual;
            this.IsSealed = symbol.IsSealed;
            this.IsOverride = symbol.IsOverride;
            this.IsAbstract = symbol.IsAbstract;
            this.IsExtensionMethod = symbol.IsExtensionMethod;
            this.IsExtern = symbol.IsExtern;

            List<AttributeApiView> attributes = new List<AttributeApiView>();
            List<TypeParameterApiView> typeParameters = new List<TypeParameterApiView>();
            List<ParameterApiView> parameters = new List<ParameterApiView>();

            foreach (AttributeData attribute in symbol.GetAttributes())
            {
                if ((attribute.AttributeClass.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public || 
                    attribute.AttributeClass.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Protected) &&
                    !ignoredAttributeNames.Contains(attribute.AttributeClass.Name) &&
                    !attribute.AttributeClass.IsImplicitlyDeclared)
                {
                    attributes.Add(new AttributeApiView(attribute, this.Id));
                }
            }
            foreach (ITypeParameterSymbol typeParam in symbol.TypeParameters)
            {
                typeParameters.Add(new TypeParameterApiView(typeParam));
            }
            foreach (IParameterSymbol param in symbol.Parameters)
            {
                parameters.Add(new ParameterApiView(param));
            }

            this.Attributes = attributes.ToArray();
            this.TypeParameters = typeParameters.ToArray();
            this.Parameters = parameters.ToArray();
        }

        public override string ToString()
        {
            var renderer = new TextRendererApiView();
            var list = new StringListApiView();
            renderer.Render(this, list);
            return list.ToString();
        }
    }
}
