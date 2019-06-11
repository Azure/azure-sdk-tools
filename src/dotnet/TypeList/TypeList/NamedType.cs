using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace TypeList
{
    public class NamedType
    {
        private const int indentSize = 4;

        private readonly string Name;
        private readonly string Type;
        private readonly string EnumUnderlyingType = null;

        private readonly ImmutableArray<Event> Events;
        private readonly ImmutableArray<Field> Fields;
        private readonly ImmutableArray<string> Implementations;
        private readonly ImmutableArray<Method> Methods;
        private readonly ImmutableArray<NamedType> NamedTypes;
        private readonly ImmutableArray<Property> Properties;

        /// <summary>
        /// Construct a new namedType instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the named type.</param>
        public NamedType(INamedTypeSymbol symbol)
        {
            this.Name = symbol.Name;
            this.Type = symbol.TypeKind.ToString().ToLower();
            if (symbol.EnumUnderlyingType != null)
                this.EnumUnderlyingType = symbol.EnumUnderlyingType.ToDisplayString();

            List<Event> events = new List<Event>();
            List<Field> fields = new List<Field>();
            List<string> implementations = new List<string>();
            List<Method> methods = new List<Method>();
            List<NamedType> namedTypes = new List<NamedType>();
            List<Property> properties = new List<Property>();

            // add any types declared in the body of this type to lists
            foreach (var memberSymbol in symbol.GetMembers())
            {
                if (memberSymbol.DeclaredAccessibility != Accessibility.Public) continue;

                switch (memberSymbol)
                {
                    case IEventSymbol e:
                        events.Add(new Event(e));
                        break;

                    case IFieldSymbol f:
                        fields.Add(new Field(f));
                        break;

                    case IMethodSymbol m:
                        bool autoMethod = false;
                        if (m.AssociatedSymbol != null)
                            autoMethod = (m.AssociatedSymbol.Kind == SymbolKind.Event) || (m.AssociatedSymbol.Kind == SymbolKind.Property);

                        if (!(m.Name.Equals(".ctor") || autoMethod))
                            methods.Add(new Method(m));
                        break;

                    case INamedTypeSymbol n:
                        namedTypes.Add(new NamedType(n));
                        break;

                    case IPropertySymbol p:
                        properties.Add(new Property(p));
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

        public string GetName()
        {
            return Name;
        }

        public string GetNamedType()
        {
            return Type;
        }

        public string GetEnumUnderlyingType()
        {
            return EnumUnderlyingType;
        }

        public ImmutableArray<Event> GetEvents()
        {
            return Events;
        }

        public ImmutableArray<Field> GetFields()
        {
            return Fields;
        }

        public ImmutableArray<string> GetImplementations()
        {
            return Implementations;
        }

        public ImmutableArray<Method> GetMethods()
        {
            return Methods;
        }

        public ImmutableArray<NamedType> GetNamedTypes()
        {
            return NamedTypes;
        }

        public ImmutableArray<Property> GetProperties()
        {
            return Properties;
        }

        public string RenderNamedType(int indents = 0)
        {
            string indent = new string(' ', indents * indentSize);

            StringBuilder returnString = new StringBuilder(indent + "public " + Type + " " + Name + " ");

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
            foreach (Field f in Fields)
            {
                returnString.Append(f.RenderField(indents + 1) + "\n");
            }
            foreach (Property p in Properties)
            {
                returnString.Append(p.RenderProperty(indents + 1) + "\n");
            }
            foreach (Event e in Events)
            {
                returnString.Append(e.RenderEvent(indents + 1) + "\n");
            }
            foreach (Method m in Methods)
            {
                returnString.Append(m.RenderMethod(indents + 1) + "\n");
            }
            foreach (NamedType n in NamedTypes)
            {
                returnString.Append(n.RenderNamedType(indents + 1) + "\n");
            }

            returnString.Append(indent + "}");

            return returnString.ToString();
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
            foreach (Field f in Fields)
            {
                returnString.Append(f.ToString() + "\n");
            }
            foreach (Property p in Properties)
            {
                returnString.Append(p.ToString() + "\n");
            }
            foreach (Event e in Events)
            {
                returnString.Append(e.ToString() + "\n");
            }
            foreach (Method m in Methods)
            {
                returnString.Append(m.ToString() + "\n");
            }
            foreach (NamedType n in NamedTypes)
            {
                returnString.Append(n.ToString() + "\n");
            }

            returnString.Append("}");

            return returnString.ToString();
        }
    }
}