using Microsoft.CodeAnalysis;
using System;
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
        public IMethodSymbol Symbol { get; }

        public string Name { get; }
        public string ReturnType { get; }

        public bool IsStatic { get; }
        public bool IsVirtual { get; }
        public bool IsSealed { get; }
        public bool IsOverride { get; }
        public bool IsAbstract { get; }
        public bool IsExtern { get; }

        public ImmutableArray<AttributeData> Attributes { get; }  //TODO: determine how to obtain all attribute info and display in string
        public ImmutableArray<Parameter> Parameters { get; }
        public ImmutableArray<TypeParameter> TypeParameters { get; }

        /// <summary>
        /// Construct a new Method instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the method.</param>
        public Method(IMethodSymbol symbol)
        {
            this.Symbol = symbol;

            this.Name = symbol.Name;
            this.ReturnType = symbol.ReturnType.ToString();

            this.IsStatic = symbol.IsStatic;
            this.IsVirtual = symbol.IsVirtual;
            this.IsSealed = symbol.IsSealed;
            this.IsOverride = symbol.IsOverride;
            this.IsAbstract = symbol.IsAbstract;
            this.IsExtern = symbol.IsExtern;

            this.Attributes = symbol.GetAttributes();

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

        public override string ToString()
        {
            bool interfaceMethod = Symbol.ContainingType.TypeKind.ToString().ToLower().Equals("interface");

            StringBuilder returnString = new StringBuilder("");
            if (!Attributes.IsEmpty)
                returnString.Append("[" + Attributes[0].AttributeClass.Name + "] ");

            if (!interfaceMethod)
                returnString.Append("public");

            if (IsStatic)
                returnString.Append(" static");
            if (IsVirtual)
                returnString.Append(" virtual");
            if (IsSealed)
                returnString.Append(" sealed");
            if (IsOverride)
                returnString.Append(" override");
            if (IsAbstract && !interfaceMethod)
                returnString.Append(" abstract");
            if (IsExtern)
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