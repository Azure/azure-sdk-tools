using Microsoft.CodeAnalysis;
using ApiView;
using Xunit;

namespace APIViewTest
{
    public class AssemblyTests
    {
        [Fact]
        public void AssemblyTestAssemblyFromSymbol()
        {
            IAssemblySymbol assemblySymbol = TestResource.GetAssemblySymbol();
            AssemblyApiv assembly = new AssemblyApiv(assemblySymbol);
            
            Assert.Equal("TestLibrary", assembly.Name);

            Assert.Single(assembly.GlobalNamespace.Namespaces);
        }

        [Fact]
        public void AssemblyTestAssembliesFromFile()
        {
            AssemblyApiv assembly = AssemblyApiv.AssemblyFromFile("TestLibrary.dll");
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceApiv globalNamespace = assembly.GlobalNamespace;
            Assert.Single(globalNamespace.Namespaces);
        }
    }
}
