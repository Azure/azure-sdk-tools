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
            AssemblyApiView assembly = new AssemblyApiView(assemblySymbol);
            
            Assert.Equal("TestLibrary", assembly.Name);

            Assert.Single(assembly.GlobalNamespace.Namespaces);
        }

        [Fact]
        public void AssemblyTestAssembliesFromFile()
        {
            AssemblyApiView assembly = AssemblyApiView.AssemblyFromFile("TestLibrary.dll");
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceApiView globalNamespace = assembly.GlobalNamespace;
            Assert.Single(globalNamespace.Namespaces);
        }
    }
}
