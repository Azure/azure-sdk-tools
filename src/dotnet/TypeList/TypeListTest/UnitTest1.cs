using System;
using Xunit;

namespace TypeListTest
{
    public class UnitTest1
    {
        /**
         * Testing strategy for each type variant in TypeList:
         * 
         * Partition the inputs as follows:
         * 
         * Assembly:
         *    Assembly(IAssemblySymbol symbol):
         *       symbol.GlobalNamespace contains: 0 namespaces, 1 namespace, > 1 namespace
         * 
         * Event:
         *    Event(IEventSymbol symbol):
         *       ensure Event instance is created out of symbol
         * 
         * Field:
         *    Field(IFieldSymbol symbol):
         *       ensure Field instance is created out of symbol
         * 
         * Method:
         *    Method(IMethodSymbol symbol):
         *       symbol.GetAttributes().Length: 0, 1, > 1
         *       symbol.IsAsync: true, false
         *       symbol.IsVirtual: true, false
         *       symbol.IsSealed: true, false
         *       symbol.IsOverride: true, false
         *       symbol.IsAbstract: true, false
         *       symbol.IsExtern: true, false
         *       symbol.IsAsync: true, false
         *       symbol.ReturnType: void, non-void
         *       symbol.TypeParameters.Length: 0, 1, > 1
         *       symbol.Parameters.Length: 0, 1, > 1
         * 
         * NamedType:
         *    NamedType(INamedTypeSymbol symbol):
         *       events in symbol.GetMembers(): 0, > 0
         *       fields in symbol.GetMembers(): 0, > 0
         *       methods in symbol.GetMembers(): 0, > 0
         *       named types in symbol.GetMembers(): 0, > 0
         * 
         * Namespace:
         *    Namespace(INamespaceSymbol symbol):
         *       named types in symbol.GetMembers(): 0, > 0
         *       namespaces in symbol.GetMembers(): 0, > 0
         * 
         * Parameter:
         *    Parameter(IParameterSymbol symbol):
         *       symbol.RefKind: None, not None
         *       symbol.HasExplicitDefaultValue: true, false
         * 
         * TypeParameter:
         *    TypeParameter(ITypeParameterSymbol symbol):
         *       symbol.GetAttributes().Length: 0, 1, > 1
         * 
         * Covering each part of each input/output partition.
         */

        // testing DLL: C:\Users\t-mcpat\Documents\azure-sdk-tools\artifacts\bin\TestLibrary\Debug\netstandard2.0\TestLibrary.dll

        [Fact]
        public void Test1()
        {
            
        }
    }
}
