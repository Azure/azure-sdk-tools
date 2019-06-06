using Microsoft.CodeAnalysis;
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
        private readonly string name;
        private readonly string returnType;

        private readonly bool isStatic;
        private readonly bool isVirtual;
        private readonly bool isSealed;
        private readonly bool isOverride;
        private readonly bool isAbstract;
        private readonly bool isExtern;
        private readonly bool isAsync;

        private readonly ImmutableArray<AttributeData> attributes;
        private readonly ImmutableArray<Parameter> parameters;
        private readonly ImmutableArray<TypeParameter> typeParameters;

        /// <summary>
        /// Construct a new Method instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the method.</param>
        public Method(IMethodSymbol symbol)
        {
            this.name = symbol.Name;
            this.returnType = symbol.ReturnType.ToString();

            this.isStatic = symbol.IsStatic;
            this.isVirtual = symbol.IsVirtual;
            this.isSealed = symbol.IsSealed;
            this.isOverride = symbol.IsOverride;
            this.isAbstract = symbol.IsAbstract;
            this.isExtern = symbol.IsExtern;
            this.isAsync = symbol.IsAsync;

            this.attributes = symbol.GetAttributes();
            Collection<TypeParameter> typeParameters = new Collection<TypeParameter>();
            Collection<Parameter> parameters = new Collection<Parameter>();

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

        public bool IsAsync()
        {
            return isAsync;
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

        public override string ToString()
        {
            StringBuilder returnString = new StringBuilder("");
            if (!attributes.IsEmpty)
                returnString.Append(attributes + " ");

            returnString.Append("public");

            if (isStatic)
                returnString.Append(" static");
            if (isVirtual)
                returnString.Append(" virtual");
            if (isSealed)
                returnString.Append(" sealed");
            if (isOverride)
                returnString.Append(" override");
            if (isAbstract)
                returnString.Append(" abstract");
            if (isExtern)
                returnString.Append(" extern");
            if (isAsync)
                returnString.Append(" async");

            returnString.Append(" " + returnType + " " + name);
            if (typeParameters.Length != 0)
            {
                returnString.Append(" <");
                foreach (TypeParameter tp in typeParameters)
                {
                    returnString.Append(tp.ToString() + ", ");
                }
                returnString.Length = returnString.Length - 2;
                returnString.Append("> ");
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
            returnString.Append(") { }\n");

            return returnString.ToString();
        }
    }
}