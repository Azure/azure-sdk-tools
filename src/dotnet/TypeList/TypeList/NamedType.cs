using Microsoft.CodeAnalysis;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;

namespace TypeList
{
    public class NamedType
    {
        private const int INDENT_SIZE = 4;

        private readonly string name;
        private readonly string type;
        private readonly string enumUnderlyingType = null;

        private readonly ImmutableArray<Event> events;
        private readonly ImmutableArray<Field> fields;
        private readonly ImmutableArray<string> implementations;
        private readonly ImmutableArray<Method> methods;
        private readonly ImmutableArray<NamedType> namedTypes;
        private readonly ImmutableArray<Property> properties;

        /// <summary>
        /// Construct a new NamedType instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the named type.</param>
        public NamedType(INamedTypeSymbol symbol)
        {
            this.name = symbol.Name;
            this.type = symbol.TypeKind.ToString().ToLower();
            if (symbol.EnumUnderlyingType != null)
                this.enumUnderlyingType = symbol.EnumUnderlyingType.ToDisplayString();

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
                        bool eventMethod = false;
                        if (m.AssociatedSymbol != null)
                            eventMethod = m.AssociatedSymbol.Kind == SymbolKind.Event;

                        if (!(m.Name.Equals(".ctor") || eventMethod))
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

            this.events = events.ToImmutableArray();
            this.fields = fields.ToImmutableArray();
            this.implementations = implementations.ToImmutableArray();
            this.methods = methods.ToImmutableArray();
            this.namedTypes = namedTypes.ToImmutableArray();
            this.properties = properties.ToImmutableArray();
        }

        public string GetName()
        {
            return name;
        }

        public string GetNamedType()
        {
            return type;
        }

        public string GetEnumUnderlyingType()
        {
            return enumUnderlyingType;
        }

        public ImmutableArray<Event> GetEvents()
        {
            return events;
        }

        public ImmutableArray<Field> GetFields()
        {
            return fields;
        }

        public ImmutableArray<string> GetImplementations()
        {
            return implementations;
        }

        public ImmutableArray<Method> GetMethods()
        {
            return methods;
        }

        public ImmutableArray<NamedType> GetNamedTypes()
        {
            return namedTypes;
        }

        public ImmutableArray<Property> GetProperties()
        {
            return properties;
        }

        public string RenderNamedType(int indents = 0)
        {
            string indent = new string(' ', indents * INDENT_SIZE);

            StringBuilder returnString = new StringBuilder(indent + "public " + type + " " + name + " ");

            // add any implemented types to string
            if (implementations.Length > 0)
            {
                returnString.Append(": ");
                foreach (var i in implementations)
                {
                    returnString.Append(i + ", ");
                }
                returnString.Length = returnString.Length - 2;
                returnString.Append(" ");
            }
            returnString.Append("{\n\n");

            // add any types declared in this type's body
            foreach (Field f in fields)
            {
                returnString.Append(indent + f.RenderField(indents + 1) + "\n");
            }
            foreach (Property p in properties)
            {
                returnString.Append(indent + p.RenderProperty(indents + 1) + "\n");
            }
            foreach (Event e in events)
            {
                returnString.Append(indent + e.RenderEvent(indents + 1) + "\n");
            }
            foreach (Method m in methods)
            {
                returnString.Append(indent + m.RenderMethod(indents + 1) + "\n");
            }
            foreach (NamedType n in namedTypes)
            {
                returnString.Append(indent + n.RenderNamedType(indents + 1) + "\n");
            }

            returnString.Append(indent + "}\n");

            return returnString.ToString();
        }

        public override string ToString()
        {
            StringBuilder returnString = new StringBuilder("public " + type + " " + name + " ");

            // add any implemented types to string
            if (implementations.Length > 0)
            {
                returnString.Append(": ");
                foreach (var i in implementations)
                {
                    returnString.Append(i + ", ");
                }
                returnString.Length = returnString.Length - 2;
                returnString.Append(" ");
            }
            returnString.Append("{\n\n");

            // add any types declared in this type's body
            foreach (Field f in fields)
            {
                returnString.Append(f.ToString() + "\n");
            }
            foreach (Property p in properties)
            {
                returnString.Append(p.ToString() + "\n");
            }
            foreach (Event e in events)
            {
                returnString.Append(e.ToString() + "\n");
            }
            foreach (Method m in methods)
            {
                returnString.Append(m.ToString() + "\n");
            }
            foreach (NamedType n in namedTypes)
            {
                returnString.Append(n.ToString() + "\n");
            }

            returnString.Append("}\n");

            return returnString.ToString();
        }
    }
}