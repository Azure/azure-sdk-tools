using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace ApiView
{
    /// <summary>
    /// Class representing a C# named type (class, interface, delegate, enum, or struct). 
    /// A named type can have a name, type, enum underlying type, events, fields, implemented 
    /// classes/interfaces, methods, properties, type parameters, and/or other named types.
    /// </summary>
    public class NamedTypeApiView
    {
        /// <summary>
        /// A unique identifier of this named type within the scope of the containing assembly.
        /// </summary>
        public string Id { get; set; }
        public string Name { get; set; }
        /// <summary>
        /// The type of the named type - class, interface, enum, etc.
        /// </summary>
        public string TypeKind { get; set; }
        /// <summary>
        /// The underlying type of the enum, if the named type is one. Otherwise, null.
        /// </summary>
        public TypeReferenceApiView EnumUnderlyingType { get; set; }
        public string Accessibility { get; set; }
        public bool IsSealed { get; set; }
        public bool IsStatic { get; set; }

        public EventApiView[]  Events { get; set; }
        public FieldApiView[] Fields { get; set; }
        public TypeReferenceApiView[] Implementations { get; set; }
        public MethodApiView[] Methods { get; set; }
        public NamedTypeApiView[] NamedTypes { get; set; }
        public PropertyApiView[] Properties { get; set; }
        public TypeParameterApiView[] TypeParameters { get; set; }

        public NamedTypeApiView() { }

        /// <summary>
        /// Construct a new NamedTypeApiView instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the named type.</param>
        public NamedTypeApiView(INamedTypeSymbol symbol)
        {
            this.Name = symbol.Name;
            this.TypeKind = symbol.TypeKind.ToString().ToLower();
            if (symbol.EnumUnderlyingType != null)
                this.EnumUnderlyingType = new TypeReferenceApiView(symbol.EnumUnderlyingType);
            this.Accessibility = symbol.DeclaredAccessibility.ToString().ToLower();
            if (this.TypeKind == "class")
                this.IsSealed = symbol.IsSealed;
            else
                this.IsSealed = false;
            this.IsStatic = symbol.IsStatic;
            this.Id = symbol.ConstructedFrom.ToDisplayString();

            var events = new List<EventApiView>();
            var fields = new List<FieldApiView>();
            var implementations = new List<TypeReferenceApiView>();
            var methods = new List<MethodApiView>();
            var namedTypes = new List<NamedTypeApiView>();
            var properties = new List<PropertyApiView>();
            var typeParameters = new List<TypeParameterApiView>();

            // add any types declared in the body of this type to lists
            foreach (var memberSymbol in symbol.GetMembers())
            {
                if (memberSymbol.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public || 
                    memberSymbol.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Protected)
                {
                    switch (memberSymbol)
                    {
                        case IEventSymbol e:
                            events.Add(new EventApiView(e));
                            break;

                        case IFieldSymbol f:
                            fields.Add(new FieldApiView(f));
                            break;

                        case IMethodSymbol m:
                            bool autoMethod = false;
                            if (m.AssociatedSymbol != null)
                                autoMethod = (m.AssociatedSymbol.Kind == SymbolKind.Event) || (m.AssociatedSymbol.Kind == SymbolKind.Property);

                            if (!((m.MethodKind == MethodKind.Constructor && m.Parameters.Length == 0) || autoMethod))
                                methods.Add(new MethodApiView(m));
                            break;

                        case INamedTypeSymbol n:
                            namedTypes.Add(new NamedTypeApiView(n));
                            break;

                        case IPropertySymbol p:
                            properties.Add(new PropertyApiView(p));
                            break;
                    }
                }
            }

            if (symbol.BaseType != null && !(symbol.BaseType.SpecialType == SpecialType.System_Object || symbol.BaseType.SpecialType == SpecialType.System_ValueType))
                implementations.Add(new TypeReferenceApiView(symbol.BaseType));

            // add a string representation of each implemented type to list
            foreach (var i in symbol.Interfaces)
            {
                implementations.Add(new TypeReferenceApiView(i));
            }

            foreach (var t in symbol.TypeParameters)
            {
                typeParameters.Add(new TypeParameterApiView(t));
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
            var renderer = new TextRendererApiView();
            var list = new StringListApiView();
            renderer.Render(this, list);
            return list.ToString();
        }
    }
}
