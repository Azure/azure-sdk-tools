using System;
using System.Collections.ObjectModel;
using System.Reflection;
using Xunit;
using Assembly = TypeList.Assembly;
using Event = TypeList.Event;
using Field = TypeList.Field;
using Method = TypeList.Method;
using NamedType = TypeList.NamedType;
using Namespace = TypeList.Namespace;
using Parameter = TypeList.Parameter;
using TypeParameter = TypeList.TypeParameter;

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
         *       symbol.IsStatic: true, false
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
         *       symbol.TypeKind: class, interface
         *       events in symbol.GetMembers(): 0, > 0
         *       fields in symbol.GetMembers(): 0, > 0
         *       methods in symbol.GetMembers(): 0, > 0
         *       named types in symbol.GetMembers(): 0, > 0
         * 
         * Namespace:
         *    Namespace(INamespaceSymbol symbol):
         *       symbol: global namespace, non-global namespace
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
        public void AssemblyTestNoNamespacesUnderGlobalNamespace()
        {
            //TODO: requires separate DLL
        }

        [Fact]
        public void AssemblyTestOneNamespaceUnderGlobalNamespace()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.GetName());

            Namespace globalNamespace = assembly.GetGlobalNamespace();
            Assert.Single(globalNamespace.GetNamespaces());
        }

        [Fact]
        public void AssemblyTestMultipleNamespacesUnderGlobalNamespace()
        {
            //TODO: requires separate DLL
        }

        [Fact]
        public void EventTestCreation()
        {
            //TODO
        }

        [Fact]
        public void FieldTestCreation()
        {
            //TODO
        }

        [Fact]
        public void MethodTestNoAttributesOneTypeParamMultipleParams()
        {
            //TODO
        }

        [Fact]
        public void MethodTestOneAttributeNoTypeParamsOneParam()
        {
            //TODO
        }

        [Fact]
        public void MethodTestMultipleAttributesMultipleTypeParamsNoParams()
        {
            //TODO
        }

        [Fact]
        public void NamedTypeTestClassSomeEventsSomeFieldsNoMethodsSomeNamedTypes()
        {
            //TODO
        }

        [Fact]
        public void NamedTypeTestInterfaceNoEventsNoFieldsSomeMethodsNoNamedTypes()
        {
            //TODO
        }

        [Fact]
        public void NamespaceTestGlobalNoNamedTypesSomeNamespaces()
        {
            //TODO
        }

        [Fact]
        public void NamespaceTestNonGlobalSomeNamedTypesNoNamespaces()
        {
            //TODO
        }

        [Fact]
        public void ParameterTestNoRefKindSomeDefaultValue()
        {
            //TODO
        }

        [Fact]
        public void ParameterTestSomeRefKindNoDefaultValue()
        {
            //TODO
        }

        [Fact]
        public void TypeParameterTestNoAttributes()
        {
            //TODO
        }

        [Fact]
        public void TypeParameterTestOneAttribute()
        {
            //TODO
        }

        [Fact]
        public void TypeParameterTestMultipleAttributes()
        {
            //TODO
        }
    }
}
