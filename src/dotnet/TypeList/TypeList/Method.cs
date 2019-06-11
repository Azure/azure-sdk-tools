using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace TypeList
{
    /// <summary>
    /// Class representing a C# method. Each method includes a name, return type, attributes, 
    /// modifiers, type parameters, and parameters.
    /// 
    /// Method is an immutable, thread-safe type.
    /// </summary>
    public class Method
    {
        private const int indentSize = 4;

        private readonly IMethodSymbol Symbol;

        private readonly string Name;
        private readonly string ReturnType;

        private readonly bool Static;
        private readonly bool Virtual;
        private readonly bool Sealed;
        private readonly bool Override;
        private readonly bool Abstract;
        private readonly bool Extern;

        private readonly ImmutableArray<AttributeData> Attributes;  //TODO: determine how to obtain all attribute info and display in string
        private readonly ImmutableArray<Parameter> Parameters;
        private readonly ImmutableArray<TypeParameter> TypeParameters;

        /// <summary>
        /// Construct a new Method instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the method.</param>
        public Method(IMethodSymbol symbol)
        {
            this.Symbol = symbol;

            this.Name = symbol.Name;
            this.ReturnType = symbol.ReturnType.ToString();

            this.Static = symbol.IsStatic;
            this.Virtual = symbol.IsVirtual;
            this.Sealed = symbol.IsSealed;
            this.Override = symbol.IsOverride;
            this.Abstract = symbol.IsAbstract;
            this.Extern = symbol.IsExtern;

            this.Attributes = symbol.GetAttributes();
#if DEBUG
            if (Attributes.Length > 0)
                Extern = false;
#endif
            List<TypeParameter> typeParameters = new List<TypeParameter>();
            List<Parameter> parameters = new List<Parameter>();

            foreach (ITypeParameterSymbol typeParam in symbol.TypeParameters)
            {
                typeParameters.Add(new TypeParameter(typeParam));
            }
            foreach (IParameterSymbol param in symbol.Parameters)
            {
                parameters.Add(new Parameter(param));
            }

            this.TypeParameters = typeParameters.ToImmutableArray();
            this.Parameters = parameters.ToImmutableArray();
        }

        public string GetName()
        {
            return Name;
        }

        public string GetReturnType()
        {
            return ReturnType;
        }

        public bool IsStatic()
        {
            return Static;
        }

        public bool IsVirtual()
        {
            return Virtual;
        }

        public bool IsSealed()
        {
            return Sealed;
        }

        public bool IsOverride()
        {
            return Override;
        }

        public bool IsAbstract()
        {
            return Abstract;
        }

        public bool IsExtern()
        {
            return Extern;
        }

        public ImmutableArray<AttributeData> GetAttributes()
        {
            return Attributes;
        }

        public ImmutableArray<Parameter> GetParameters()
        {
            return Parameters;
        }

        public ImmutableArray<TypeParameter> GetTypeParameters()
        {
            return TypeParameters;
        }

        public string RenderMethod(int indents = 0)
        {
            string indent = new string(' ', indents * indentSize);

            bool interfaceMethod = Symbol.ContainingType.TypeKind.ToString().ToLower().Equals("interface");

            StringBuilder returnString = new StringBuilder(indent);
            if (!Attributes.IsEmpty)
                returnString.Append("[" + Attributes[0].AttributeClass.Name + "]\n" + indent);

            if (!interfaceMethod)
                returnString.Append("public");

            if (Static)
                returnString.Append(" static");
            if (Virtual)
                returnString.Append(" virtual");
            if (Sealed)
                returnString.Append(" sealed");
            if (Override)
                returnString.Append(" override");
            if (Abstract && !interfaceMethod)
                returnString.Append(" abstract");
            if (Extern)
                returnString.Append(" extern");

            returnString.Append(" " + ReturnType + " " + Name);
            if (TypeParameters.Length != 0)
            {
                returnString.Append("<");
                foreach (TypeParameter tp in TypeParameters)
                {
                    returnString.Append(tp.RenderTypeParameter() + ", ");
                }
                returnString.Length = returnString.Length - 2;
                returnString.Append(">");
            }

            returnString.Append("(");
            if (Parameters.Length != 0)
            {
                foreach (Parameter p in Parameters)
                {
                    returnString.Append(p.RenderParameter() + ", ");
                }
                returnString.Length = returnString.Length - 2;
            }

            if (interfaceMethod)
                returnString.Append(");");
            else
                returnString.Append(") { }");

            return returnString.ToString();
        }

        public override string ToString()
        {
            bool interfaceMethod = Symbol.ContainingType.TypeKind.ToString().ToLower().Equals("interface");

            StringBuilder returnString = new StringBuilder("");
            if (!Attributes.IsEmpty)
                returnString.Append("[" + Attributes[0].AttributeClass.Name + "] ");

            if (!interfaceMethod)
                returnString.Append("public");

            if (Static)
                returnString.Append(" static");
            if (Virtual)
                returnString.Append(" virtual");
            if (Sealed)
                returnString.Append(" sealed");
            if (Override)
                returnString.Append(" override");
            if (Abstract && !interfaceMethod)
                returnString.Append(" abstract");
            if (Extern)
                returnString.Append(" extern");

            returnString.Append(" " + ReturnType + " " + Name);
            if (TypeParameters.Length != 0)
            {
                returnString.Append("<");
                foreach (TypeParameter tp in TypeParameters)
                {
                    returnString.Append(tp.ToString() + ", ");
                }
                returnString.Length = returnString.Length - 2;
                returnString.Append(">");
            }

            returnString.Append("(");
            if (Parameters.Length != 0)
            {
                foreach (Parameter p in Parameters)
                {
                    returnString.Append(p.ToString() + ", ");
                }
                returnString.Length = returnString.Length - 2;
            }

            if (interfaceMethod)
                returnString.Append(");");
            else
                returnString.Append(") { }");

            return returnString.ToString();
        }
    }
}