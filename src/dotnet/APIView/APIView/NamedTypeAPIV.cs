using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace ApiView
{
    /// <summary>
    /// Class representing a C# named type (class, interface, delegate, enum, or struct). 
    /// A named type can have a name, type, enum underlying type, events, fields, implemented 
    /// classes/interfaces, methods, properties, type parameters, and/or other named types.
    /// </summary>
    public class NamedTypeApiv
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string TypeKind { get; set; }
        public TypeReferenceApiv EnumUnderlyingType { get; set; }
        public string Accessibility { get; set; }
        public bool IsSealed { get; set; }
        public bool IsStatic { get; set; }

        public EventApiv[]  Events { get; set; }
        public FieldApiv[] Fields { get; set; }
        public TypeReferenceApiv[] Implementations { get; set; }
        public MethodApiv[] Methods { get; set; }
        public NamedTypeApiv[] NamedTypes { get; set; }
        public PropertyApiv[] Properties { get; set; }
        public TypeParameterApiv[] TypeParameters { get; set; }

        public NamedTypeApiv() { }

        /// <summary>
        /// Construct a new NamedTypeApiv instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the named type.</param>
        public NamedTypeApiv(INamedTypeSymbol symbol)
        {
            this.Name = symbol.Name;
            this.TypeKind = symbol.TypeKind.ToString().ToLower();
            if (symbol.EnumUnderlyingType != null)
                this.EnumUnderlyingType = new TypeReferenceApiv(symbol.EnumUnderlyingType);
            this.Accessibility = symbol.DeclaredAccessibility.ToString().ToLower();
            this.IsSealed = symbol.IsSealed;
            this.IsStatic = symbol.IsStatic;
            this.Id = symbol.ConstructedFrom.ToDisplayString();

            var events = new List<EventApiv>();
            var fields = new List<FieldApiv>();
            var implementations = new List<TypeReferenceApiv>();
            var methods = new List<MethodApiv>();
            var namedTypes = new List<NamedTypeApiv>();
            var properties = new List<PropertyApiv>();
            var typeParameters = new List<TypeParameterApiv>();

            // add any types declared in the body of this type to lists
            foreach (var memberSymbol in symbol.GetMembers())
            {
                if (memberSymbol.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public || 
                    memberSymbol.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Protected)
                {
                    switch (memberSymbol)
                    {
                        case IEventSymbol e:
                            events.Add(new EventApiv(e));
                            break;

                        case IFieldSymbol f:
                            fields.Add(new FieldApiv(f));
                            break;

                        case IMethodSymbol m:
                            bool autoMethod = false;
                            if (m.AssociatedSymbol != null)
                                autoMethod = (m.AssociatedSymbol.Kind == SymbolKind.Event) || (m.AssociatedSymbol.Kind == SymbolKind.Property);

                            if (!((m.MethodKind == MethodKind.Constructor && m.Parameters.Length == 0) || autoMethod))
                                methods.Add(new MethodApiv(m));
                            break;

                        case INamedTypeSymbol n:
                            namedTypes.Add(new NamedTypeApiv(n));
                            break;

                        case IPropertySymbol p:
                            properties.Add(new PropertyApiv(p));
                            break;
                    }
                }
            }

            if (symbol.BaseType != null && !(symbol.BaseType.SpecialType == SpecialType.System_Object || symbol.BaseType.SpecialType == SpecialType.System_ValueType))
                implementations.Add(new TypeReferenceApiv(symbol.BaseType));

            // add a string representation of each implemented type to list
            foreach (var i in symbol.Interfaces)
            {
                implementations.Add(new TypeReferenceApiv(i));
            }

            foreach (var t in symbol.TypeParameters)
            {
                typeParameters.Add(new TypeParameterApiv(t));
            }

            this.Events = events.ToArray();
            this.Fields = fields.ToArray();
            this.Implementations = implementations.ToArray();
            this.Methods = methods.ToArray();
            this.NamedTypes = namedTypes.ToArray();
            this.Properties = properties.ToArray();
            this.TypeParameters = typeParameters.ToArray();
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
