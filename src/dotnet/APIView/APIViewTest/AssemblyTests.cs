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
        [Fact]
        public void AssemblyTestAssembly()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            AssemblyAPIV assembly = new AssemblyAPIV(a);
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceAPIV globalNamespace = assembly.GlobalNamespace;
            Assert.Single(globalNamespace.Namespaces);
        }

        [Fact]
        public void AssemblyTestAssembliesFromFile()
        {
            AssemblyAPIV assembly = AssemblyAPIV.AssembliesFromFile("TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceAPIV globalNamespace = assembly.GlobalNamespace;
            Assert.Single(globalNamespace.Namespaces);
        }
    }
}