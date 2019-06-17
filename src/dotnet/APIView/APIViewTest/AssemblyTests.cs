using Microsoft.CodeAnalysis;
using APIView;
using Xunit;

namespace APIViewTest
{
    public class AssemblyTests
    {
        [Fact]
        public void AssemblyTestAssembly()
        {
            IAssemblySymbol assemblySymbol = TestResource.GetAssemblySymbol();
            AssemblyAPIV assembly = new AssemblyAPIV(assemblySymbol);
            
            Assert.Equal("TestLibrary", assembly.Name);

            Assert.Single(assembly.GlobalNamespace.Namespaces);
        }

        [Fact]
        public void AssemblyTestAssembliesFromFile()
        {
            AssemblyAPIV assembly = null;
            foreach (AssemblyAPIV a in AssemblyAPIV.AssembliesFromFile("TestLibrary.dll"))
            {
                if (a.Name.Equals("TestLibrary"))
                    assembly = a;
            }
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceAPIV globalNamespace = assembly.GlobalNamespace;
            Assert.Single(globalNamespace.Namespaces);
        }
    }
}
