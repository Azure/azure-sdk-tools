using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace APIView
{
    public class NamedTypeAPIV
    {
        public string Name { get; }
        public string Type { get; }
        public string EnumUnderlyingType { get; }

        public ImmutableArray<EventAPIV> Events { get; }
        public ImmutableArray<FieldAPIV> Fields { get; }
        public ImmutableArray<string> Implementations { get; }
        public ImmutableArray<MethodAPIV> Methods { get; }
        public ImmutableArray<NamedTypeAPIV> NamedTypes { get; }
        public ImmutableArray<PropertyAPIV> Properties { get; }

        /// <summary>
        /// Construct a new namedType instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the named type.</param>
        public NamedTypeAPIV(INamedTypeSymbol symbol)
        {
            this.Name = symbol.Name;
            this.Type = symbol.TypeKind.ToString().ToLower();
            if (symbol.EnumUnderlyingType != null)
                this.EnumUnderlyingType = symbol.EnumUnderlyingType.ToDisplayString();

            List<EventAPIV> events = new List<EventAPIV>();
            List<FieldAPIV> fields = new List<FieldAPIV>();
            List<string> implementations = new List<string>();
            List<MethodAPIV> methods = new List<MethodAPIV>();
            List<NamedTypeAPIV> namedTypes = new List<NamedTypeAPIV>();
            List<PropertyAPIV> properties = new List<PropertyAPIV>();

            // add any types declared in the body of this type to lists
            foreach (var memberSymbol in symbol.GetMembers())
            {
                if (memberSymbol.DeclaredAccessibility != Accessibility.Public) continue;

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

                        if (!(m.Name.Equals(".ctor") || autoMethod))
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

            // add a string representation of each implemented type to list
            foreach (var i in symbol.Interfaces)
            {
                StringBuilder stringRep = new StringBuilder(i.Name);
                if (i.TypeArguments.Length > 0)
                {
                    stringRep.Append("<");
                    foreach (var arg in i.TypeArguments)
                    {
                        stringRep.Append(arg.ToDisplayString() + ", ");
                    }
                    stringRep.Length = stringRep.Length - 2;
                    stringRep.Append(">");
                }
                implementations.Add(stringRep.ToString());
            }

            this.Events = events.ToImmutableArray();
            this.Fields = fields.ToImmutableArray();
            this.Implementations = implementations.ToImmutableArray();
            this.Methods = methods.ToImmutableArray();
            this.NamedTypes = namedTypes.ToImmutableArray();
            this.Properties = properties.ToImmutableArray();
        }

        public override string ToString()
        {
            StringBuilder returnString = new StringBuilder("public " + Type + " " + Name + " ");

            // add any implemented types to string
            if (Implementations.Length > 0)
            {
                returnString.Append(": ");
                foreach (var i in Implementations)
                {
                    returnString.Append(i + ", ");
                }
                returnString.Length = returnString.Length - 2;
                returnString.Append(" ");
            }
            returnString.Append("{\n");

            // add any types declared in this type's body
            foreach (FieldAPIV f in Fields)
            {
                returnString.Append(f.ToString() + "\n");
            }
            foreach (PropertyAPIV p in Properties)
            {
                returnString.Append(p.ToString() + "\n");
            }
            foreach (EventAPIV e in Events)
            {
                returnString.Append(e.ToString() + "\n");
            }
            foreach (MethodAPIV m in Methods)
            {
                returnString.Append(m.ToString() + "\n");
            }
            foreach (NamedTypeAPIV n in NamedTypes)
            {
                returnString.Append(n.ToString() + "\n");
            }

            returnString.Append("}");

            return returnString.ToString();
        }
    }
}