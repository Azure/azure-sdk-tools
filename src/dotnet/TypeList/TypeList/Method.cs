using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
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
        private const int INDENT_SIZE = 4;

        private readonly IMethodSymbol symbol;

        private readonly string name;
        private readonly string returnType;

        private readonly bool isStatic;
        private readonly bool isVirtual;
        private readonly bool isSealed;
        private readonly bool isOverride;
        private readonly bool isAbstract;
        private readonly bool isExtern;

        private readonly ImmutableArray<AttributeData> attributes;  //TODO: determine how to obtain all attribute info and display in string
        private readonly ImmutableArray<Parameter> parameters;
        private readonly ImmutableArray<TypeParameter> typeParameters;

        /// <summary>
        /// Construct a new Method instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the method.</param>
        public Method(IMethodSymbol symbol)
        {
            this.symbol = symbol;

            this.name = symbol.Name;
            this.returnType = symbol.ReturnType.ToString();

            this.isStatic = symbol.IsStatic;
            this.isVirtual = symbol.IsVirtual;
            this.isSealed = symbol.IsSealed;
            this.isOverride = symbol.IsOverride;
            this.isAbstract = symbol.IsAbstract;
            this.isExtern = symbol.IsExtern;

            this.attributes = symbol.GetAttributes();
#if DEBUG
            if (attributes.Length > 0)
                isExtern = false;
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

            this.typeParameters = typeParameters.ToImmutableArray();
            this.parameters = parameters.ToImmutableArray();
        }

        public string GetName()
        {
            return name;
        }

        public string GetReturnType()
        {
            return returnType;
        }

        public bool IsStatic()
        {
            return isStatic;
        }

        public bool IsVirtual()
        {
            return isVirtual;
        }

        public bool IsSealed()
        {
            return isSealed;
        }

        public bool IsOverride()
        {
            return isOverride;
        }

        public bool IsAbstract()
        {
            return isAbstract;
        }

        public bool IsExtern()
        {
            return isExtern;
        }

        public ImmutableArray<AttributeData> GetAttributes()
        {
            return attributes;
        }

        public ImmutableArray<Parameter> GetParameters()
        {
            return parameters;
        }

        public ImmutableArray<TypeParameter> GetTypeParameters()
        {
            return typeParameters;
        }

        public string RenderMethod(int indents = 0)
        {
            string indent = new string(' ', indents * INDENT_SIZE);

            bool interfaceMethod = symbol.ContainingType.TypeKind.ToString().ToLower().Equals("interface");

            StringBuilder returnString = new StringBuilder(indent);
            if (!attributes.IsEmpty)
                returnString.Append("[" + attributes[0].AttributeClass.Name + "] ");

            if (!interfaceMethod)
                returnString.Append("public");

            if (isStatic)
                returnString.Append(" static");
            if (isVirtual)
                returnString.Append(" virtual");
            if (isSealed)
                returnString.Append(" sealed");
            if (isOverride)
                returnString.Append(" override");
            if (isAbstract && !interfaceMethod)
                returnString.Append(" abstract");
            if (isExtern)
                returnString.Append(" extern");

            returnString.Append(" " + returnType + " " + name);
            if (typeParameters.Length != 0)
            {
                returnString.Append("<");
                foreach (TypeParameter tp in typeParameters)
                {
                    returnString.Append(tp.RenderTypeParameter() + ", ");
                }
                returnString.Length = returnString.Length - 2;
                returnString.Append(">");
            }

            returnString.Append("(");
            if (parameters.Length != 0)
            {
                foreach (Parameter p in parameters)
                {
                    returnString.Append(p.RenderParameter() + ", ");
                }
                returnString.Length = returnString.Length - 2;
            }

            if (interfaceMethod)
                returnString.Append(");\n");
            else
                returnString.Append(") { }\n");

            return returnString.ToString();
        }

        public override string ToString()
        {
            bool interfaceMethod = symbol.ContainingType.TypeKind.ToString().ToLower().Equals("interface");

            StringBuilder returnString = new StringBuilder("");
            if (!attributes.IsEmpty)
                returnString.Append("[" + attributes[0].AttributeClass.Name + "] ");

            if (!interfaceMethod)
                returnString.Append("public");

            if (isStatic)
                returnString.Append(" static");
            if (isVirtual)
                returnString.Append(" virtual");
            if (isSealed)
                returnString.Append(" sealed");
            if (isOverride)
                returnString.Append(" override");
            if (isAbstract && !interfaceMethod)
                returnString.Append(" abstract");
            if (isExtern)
                returnString.Append(" extern");

            returnString.Append(" " + returnType + " " + name);
            if (typeParameters.Length != 0)
            {
                returnString.Append("<");
                foreach (TypeParameter tp in typeParameters)
                {
                    returnString.Append(tp.ToString() + ", ");
                }
                returnString.Length = returnString.Length - 2;
                returnString.Append(">");
            }

            returnString.Append("(");
            if (parameters.Length != 0)
            {
                foreach (Parameter p in parameters)
                {
                    returnString.Append(p.ToString() + ", ");
                }
                returnString.Length = returnString.Length - 2;
            }

            if (interfaceMethod)
                returnString.Append(");\n");
            else
                returnString.Append(") { }\n");

            return returnString.ToString();
        }
    }
}