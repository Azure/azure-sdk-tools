using Microsoft.CodeAnalysis;
using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Text;

namespace TypeList
{
    internal class Method
    {
        private readonly ImmutableArray<AttributeData> attributes;

        private readonly bool isStatic;
        private readonly bool isVirtual;
        private readonly bool isSealed;
        private readonly bool isOverride;
        private readonly bool isAbstract;
        private readonly bool isExtern;
        private readonly bool isAsync;

        private readonly string name;
        private readonly string returnType;

        private readonly Collection<Parameter> parameters = new Collection<Parameter>();
        private readonly Collection<TypeParameter> typeParameters = new Collection<TypeParameter>();

        /// <summary>
        /// Construct a new Method instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the method.</param>
        public Method(IMethodSymbol symbol)
        {
            this.attributes = symbol.GetAttributes();

            this.isStatic = symbol.IsStatic;
            this.isVirtual = symbol.IsVirtual;
            this.isSealed = symbol.IsSealed;
            this.isOverride = symbol.IsOverride;
            this.isAbstract = symbol.IsAbstract;
            this.isExtern = symbol.IsExtern;
            this.isAsync = symbol.IsAsync;

            this.name = symbol.Name;
            this.returnType = symbol.ReturnType.ToString();

            foreach (ITypeParameterSymbol typeParam in symbol.TypeParameters)
            {
                this.typeParameters.Add(new TypeParameter(typeParam));
            }
            foreach (IParameterSymbol param in symbol.Parameters)
            {
                this.parameters.Add(new Parameter(param));
            }
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
            if (typeParameters.Count != 0)
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
            if (parameters.Count != 0)
            {
                foreach (Parameter p in parameters)
                {
                    returnString.Append(p.ToString() + ", ");
                }
                returnString.Length = returnString.Length - 2;
            }
            returnString.Append(") { };\n");

            return returnString.ToString();
        }
    }
}