using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Linq;
using APIView;
using Xunit;

namespace APIViewTest
{
    public class AssemblyTests
    {
        /**
         * Testing strategy for each type variant in TypeList:
         * 
         * Partition the inputs as follows:
         * 
         * Assembly:
         *    Assembly(IAssemblySymbol symbol):
         *       ensure Assembly creation
         *    AssembliesFromFile(string dllPath):
         *       ensure Assembly creation
         * 
         * Event:
         *    Event(IEventSymbol symbol):
         *       ensure Event creation
         * 
         * Field:
         *    Field(IFieldSymbol symbol):
         *       symbol.HasConstantValue: true, false
         * 
         * Method:
         *    Method(IMethodSymbol symbol):
         *       symbol.Attributes.Length: 0, 1, > 1
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
         *       symbol.TypeKind: class, interface, delegate, enum
         *       symbol declares underlying enum type: yes, no
         *       symbol declares values for enum entries: yes, no
         *       symbol implements interface: yes, no
         *       events in symbol.GetMembers(): 0, > 0
         *          count: 0, > 0
         *          access: public, private, protected, internal, protected internal, private protected
         *       fields in symbol.GetMembers(): 0, > 0
         *          count: 0, > 0
         *          access: public, private, protected, internal, protected internal, private protected
         *       methods in symbol.GetMembers(): 0, > 0
         *          count: 0, > 0
         *          access: public, private, protected, internal, protected internal, private protected
         *       named types in symbol.GetMembers():
         *          count: 0, > 0
         *          access: public, private, protected, internal, protected internal, private protected
         *       properties in symbol.GetMembers():
         *          count: 0, > 0
         *          access: public, private, protected, internal, protected internal, private protected
         * 
         * Namespace:
         *    Namespace(InamespaceSymbol symbol):
         *       symbol: global namespace, non-global namespace
         *       named types in symbol.GetMembers():
         *          count: 0, > 0
         *          access: public, internal
         *       namespaces in symbol.GetMembers(): 0, > 0
         * 
         * Parameter:
         *    Parameter(IParameterSymbol symbol):
         *       symbol.Type: has reference type, doesn't have reference type
         *       symbol.HasExplicitDefaultValue: true, false
         *       symbol.ExplicitDefaultValue.Type: string, other
         *       
         * Property:
         *    Property(IPropertySymbol symbol):
         *       symbol.SetMethod: null, not null
         * 
         * TypeParameter:
         *    TypeParameter(ITypeParameterSymbol symbol):
         *       ensure TypeParameter creation
         * 
         * Covering each part of each input/output partition.
         */

        // testing DLL: C:\Users\t-mcpat\Documents\azure-sdk-tools\artifacts\bin\TestLibrary\Debug\netcoreapp2.2\TestLibrary.dll

        [Fact]
        public void AssemblyTestAssembly()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            AssemblyAPIV assembly = new AssemblyAPIV(a);
            var eventSymbol = (IEventSymbol)a.GetTypeByMetadataName("Program.Bla").GetMembers("myEvent").Single();
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceAPIV globalNamespace = assembly.GlobalNamespace;
            Assert.Single(globalNamespace.Namespaces);
        }

        [Fact]
        public void AssemblyTestAssembliesFromFile()
        {
            AssemblyAPIV assembly = AssemblyAPIV.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceAPIV globalNamespace = assembly.GlobalNamespace;
            Assert.Single(globalNamespace.Namespaces);
        }
    }
}