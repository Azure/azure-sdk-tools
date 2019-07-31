using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Text;

namespace APIView
{
    /// <summary>
    /// Class representing a C# named type (class, interface, delegate, enum, or struct). 
    /// A named type can have a name, type, enum underlying type, events, fields, implemented 
    /// classes/interfaces, methods, properties, type parameters, and/or other named types.
    /// 
    /// NamedTypeAPIV is an immutable, thread-safe type.
    /// </summary>
    public class NamedTypeAPIV
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string TypeKind { get; set; }
        public TypeReferenceAPIV EnumUnderlyingType { get; set; }
        public string Accessibility { get; set; }
        public bool IsSealed { get; set; }
        public bool IsStatic { get; set; }

        public EventAPIV[]  Events { get; set; }
        public FieldAPIV[] Fields { get; set; }
        public TypeReferenceAPIV[] Implementations { get; set; }
        public MethodAPIV[] Methods { get; set; }
        public NamedTypeAPIV[] NamedTypes { get; set; }
        public PropertyAPIV[] Properties { get; set; }
        public TypeParameterAPIV[] TypeParameters { get; set; }

        public NamedTypeAPIV() { }

        /// <summary>
        /// Construct a new NamedTypeAPIV instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the named type.</param>
        public NamedTypeAPIV(INamedTypeSymbol symbol)
        {
            this.Name = symbol.Name;
            this.TypeKind = symbol.TypeKind.ToString().ToLower();
            if (symbol.EnumUnderlyingType != null)
                this.EnumUnderlyingType = new TypeReferenceAPIV(symbol.EnumUnderlyingType);
            this.Accessibility = symbol.DeclaredAccessibility.ToString().ToLower();
            this.IsSealed = symbol.IsSealed;
            this.IsStatic = symbol.IsStatic;
            this.Id = symbol.ConstructedFrom.ToDisplayString();

            var events = new List<EventAPIV>();
            var fields = new List<FieldAPIV>();
            var implementations = new List<TypeReferenceAPIV>();
            var methods = new List<MethodAPIV>();
            var namedTypes = new List<NamedTypeAPIV>();
            var properties = new List<PropertyAPIV>();
            var typeParameters = new List<TypeParameterAPIV>();

            // add any types declared in the body of this type to lists
            foreach (var memberSymbol in symbol.GetMembers())
            {
                if (memberSymbol.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public || 
                    memberSymbol.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Protected)
                {
                    switch (memberSymbol)
                    {
                        case IEventSymbol e:
                            events.Add(new EventAPIV(e));
                            break;

                        case IFieldSymbol f:
                            fields.Add(new FieldAPIV(f));
                            break;

                        case IMethodSymbol m:
                            bool autoMethod = false;
                            if (m.AssociatedSymbol != null)
                                autoMethod = (m.AssociatedSymbol.Kind == SymbolKind.Event) || (m.AssociatedSymbol.Kind == SymbolKind.Property);

                            if (!((m.MethodKind == MethodKind.Constructor && m.Parameters.Length == 0) || autoMethod))
                                methods.Add(new MethodAPIV(m));
                            break;

                        case INamedTypeSymbol n:
                            namedTypes.Add(new NamedTypeAPIV(n));
                            break;

                        case IPropertySymbol p:
                            properties.Add(new PropertyAPIV(p));
                            break;
                    }
                }
            }

            if (symbol.BaseType != null && !(symbol.BaseType.SpecialType == SpecialType.System_Object || symbol.BaseType.SpecialType == SpecialType.System_ValueType))
                implementations.Add(new TypeReferenceAPIV(symbol.BaseType));

            // add a string representation of each implemented type to list
            foreach (var i in symbol.Interfaces)
            {
                implementations.Add(new TypeReferenceAPIV(i));
            }

            foreach (var t in symbol.TypeParameters)
            {
                typeParameters.Add(new TypeParameterAPIV(t));
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
            var renderer = new TextRendererAPIV();
            var list = new StringListAPIV();
            renderer.Render(this, list);
            return list.ToString();
        }
    }
}
