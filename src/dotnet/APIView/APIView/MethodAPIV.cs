using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace APIView
{
    /// <summary>
    /// Class representing a C# method. Each method includes a name, return type, attributes, 
    /// modifiers, type parameters, and parameters.
    /// 
    /// Method is an immutable, thread-safe type.
    /// </summary>
    public class MethodAPIV
    {
        public string Name { get; }
        public string ReturnType { get; }

        public bool IsInterfaceMethod { get; }
        public bool IsStatic { get; }
        public bool IsVirtual { get; }
        public bool IsSealed { get; }
        public bool IsOverride { get; }
        public bool IsAbstract { get; }
        public bool IsExtern { get; }

        public ImmutableArray<AttributeData> Attributes { get; }  //TODO: determine how to obtain all attribute info and display in string
        public ImmutableArray<ParameterAPIV> Parameters { get; }
        public ImmutableArray<TypeParameterAPIV> TypeParameters { get; }

        /// <summary>
        /// Construct a new Method instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the method.</param>
        public MethodAPIV(IMethodSymbol symbol)
        {
            this.Name = symbol.Name;
            this.ReturnType = symbol.ReturnType.ToString();

            this.IsInterfaceMethod = symbol.ContainingType.TypeKind.ToString().ToLower().Equals("interface");
            this.IsStatic = symbol.IsStatic;
            this.IsVirtual = symbol.IsVirtual;
            this.IsSealed = symbol.IsSealed;
            this.IsOverride = symbol.IsOverride;
            this.IsAbstract = symbol.IsAbstract;
            this.IsExtern = symbol.IsExtern;

            this.Attributes = symbol.GetAttributes();

            List<TypeParameterAPIV> typeParameters = new List<TypeParameterAPIV>();
            List<ParameterAPIV> parameters = new List<ParameterAPIV>();

            foreach (ITypeParameterSymbol typeParam in symbol.TypeParameters)
            {
                typeParameters.Add(new TypeParameterAPIV(typeParam));
            }
            foreach (IParameterSymbol param in symbol.Parameters)
            {
                parameters.Add(new ParameterAPIV(param));
            }

            this.TypeParameters = typeParameters.ToImmutableArray();
            this.Parameters = parameters.ToImmutableArray();
        }

        public override string ToString()
        {
            StringBuilder returnString = new StringBuilder("");
            if (!Attributes.IsEmpty)
                returnString.Append("[").Append(Attributes[0].AttributeClass.Name).Append("(").Append(Attributes[0].AttributeConstructor).Append(")] ");

            if (!IsInterfaceMethod)
                returnString.Append("public");

            if (IsStatic)
                returnString.Append(" static");
            if (IsVirtual)
                returnString.Append(" virtual");
            if (IsSealed)
                returnString.Append(" sealed");
            if (IsOverride)
                returnString.Append(" override");
            if (IsAbstract && !IsInterfaceMethod)
                returnString.Append(" abstract");
            if (IsExtern)
                returnString.Append(" extern");

            returnString.Append(" " + ReturnType + " " + Name);
            if (TypeParameters.Length != 0)
            {
                returnString.Append("<");
                foreach (TypeParameterAPIV tp in TypeParameters)
                {
                    returnString.Append(tp.ToString() + ", ");
                }
                returnString.Length = returnString.Length - 2;
                returnString.Append(">");
            }

            returnString.Append("(");
            if (Parameters.Length != 0)
            {
                foreach (ParameterAPIV p in Parameters)
                {
                    returnString.Append(p.ToString() + ", ");
                }
                returnString.Length = returnString.Length - 2;
            }

            if (IsInterfaceMethod)
                returnString.Append(");");
            else
                returnString.Append(") { }");

            return returnString.ToString();
        }
    }
}