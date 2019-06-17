using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        public string Name { get; }
        public string Type { get; }
        public string EnumUnderlyingType { get; }
        public string Accessibility { get; }

        public ImmutableArray<EventAPIV> Events { get; }
        public ImmutableArray<FieldAPIV> Fields { get; }
        public ImmutableArray<string> Implementations { get; }
        public ImmutableArray<MethodAPIV> Methods { get; }
        public ImmutableArray<NamedTypeAPIV> NamedTypes { get; }
        public ImmutableArray<PropertyAPIV> Properties { get; }
        public ImmutableArray<TypeParameterAPIV> TypeParameters { get; }

        /// <summary>
        /// Construct a new NamedTypeAPIV instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the named type.</param>
        public NamedTypeAPIV(INamedTypeSymbol symbol)
        {
            this.Name = symbol.Name;
            this.Type = symbol.TypeKind.ToString().ToLower();
            if (symbol.EnumUnderlyingType != null)
                this.EnumUnderlyingType = symbol.EnumUnderlyingType.ToDisplayString();
            this.Accessibility = symbol.DeclaredAccessibility.ToString().ToLower();

            List<EventAPIV> events = new List<EventAPIV>();
            List<FieldAPIV> fields = new List<FieldAPIV>();
            List<string> implementations = new List<string>();
            List<MethodAPIV> methods = new List<MethodAPIV>();
            List<NamedTypeAPIV> namedTypes = new List<NamedTypeAPIV>();
            List<PropertyAPIV> properties = new List<PropertyAPIV>();
            List<TypeParameterAPIV> typeParameters = new List<TypeParameterAPIV>();

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
                implementations.Add(symbol.BaseType.ToDisplayString());

            // add a string representation of each implemented type to list
            foreach (var i in symbol.Interfaces)
            {
                implementations.Add(i.ToDisplayString());
            }

            foreach (var t in symbol.TypeParameters)
            {
                typeParameters.Add(new TypeParameterAPIV(t));
            }

            this.Events = events.ToImmutableArray();
            this.Fields = fields.ToImmutableArray();
            this.Implementations = implementations.ToImmutableArray();
            this.Methods = methods.ToImmutableArray();
            this.NamedTypes = namedTypes.ToImmutableArray();
            this.Properties = properties.ToImmutableArray();
            this.TypeParameters = typeParameters.ToImmutableArray();
        }

        public override string ToString()
        {
            var returnString = new StringBuilder();
            TreeRendererAPIV.RenderText(this, returnString);
            return returnString.ToString();
        }
    }
}
