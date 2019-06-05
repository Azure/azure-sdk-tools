using Microsoft.CodeAnalysis;
using System.Collections.ObjectModel;

namespace TypeList
{
    internal class NamedType
    {
        private readonly INamedTypeSymbol symbol;

        private readonly Collection<Event> events = new Collection<Event>();
        private readonly Collection<Field> fields = new Collection<Field>();
        private readonly Collection<Method> methods = new Collection<Method>();
        private readonly Collection<NamedType> namedTypes = new Collection<NamedType>();

        /// <summary>
        /// Construct a new NamedType instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the named type.</param>
        public NamedType(INamedTypeSymbol symbol)
        {
            this.symbol = symbol;

            foreach (var memberSymbol in symbol.GetMembers())
            {
                if (memberSymbol.DeclaredAccessibility != Accessibility.Public) continue;

                switch (memberSymbol)
                {
                    case IEventSymbol e:
                        this.events.Add(new Event(e));
                        break;

                    case IFieldSymbol f:
                        this.fields.Add(new Field(f));
                        break;

                    case IMethodSymbol m:
                        this.methods.Add(new Method(m));
                        break;
                }
            }
        }

        public override string ToString()
        {
            // TODO: find way to determine class vs. interface status
            string returnString = "Class/interface: " + symbol + "\n";
            
            foreach (Event e in events)
            {
                returnString += e.ToString();
            }
            foreach (Field f in fields)
            {
                returnString += f.ToString();
            }
            foreach (Method m in methods)
            {
                returnString += m.ToString();
            }
            foreach (NamedType n in namedTypes)
            {
                returnString += n.ToString();
            }

            return returnString;
        }
    }
}