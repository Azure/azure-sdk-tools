using Microsoft.CodeAnalysis;
using System;
using System.Collections.ObjectModel;

namespace TypeList
{
    internal class Method
    {
        private readonly IMethodSymbol symbol;
        private readonly MethodKind methodKind;
        private readonly ITypeSymbol returnType;

        private readonly bool isAsync;
        private readonly bool isOverride;
        private readonly bool isStatic;
        private readonly bool isVirtual;

        private readonly Collection<Parameter> parameters = new Collection<Parameter>();
        private readonly Collection<TypeArgument> typeArguments = new Collection<TypeArgument>();
        private readonly Collection<TypeParameter> typeParameters = new Collection<TypeParameter>();

        /// <summary>
        /// Construct a new Method instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the method.</param>
        public Method(IMethodSymbol symbol)
        {
            this.symbol = symbol;
            this.methodKind = symbol.MethodKind;
            this.returnType = symbol.ReturnType;

            this.isAsync = symbol.IsAsync;
            this.isOverride = symbol.IsOverride;
            this.isStatic = symbol.IsStatic;
            this.isVirtual = symbol.IsVirtual;

            foreach (ITypeSymbol argument in symbol.TypeArguments)
            {
                this.typeArguments.Add(new TypeArgument(argument));
            }
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
            string returnString = "Method: " + symbol + "\n" +
                                  "Method kind: " + methodKind + "\n" +
                                  "Return type: " + returnType + "\n";

            foreach (Parameter p in parameters)
            {
                returnString += p.ToString();
            }
            foreach (TypeParameter tp in typeParameters)
            {
                returnString += tp.ToString();
            }
            foreach (TypeArgument ta in typeArguments)
            {
                returnString += ta.ToString();
            }

            return returnString;
        }
    }
}