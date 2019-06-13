using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using APIView;
using Xunit;

namespace APIViewTest
{
    public class PropertyTests
    {
        [Fact]
        public void PropertyTestNoSetter()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var propertySymbol = (IPropertySymbol)a.GetTypeByMetadataName("TestLibrary.PublicClass").GetMembers("propertyGet").Single();
            PropertyAPIV property = new PropertyAPIV(propertySymbol);
            
            Assert.Equal("propertyGet", property.Name);
            Assert.Equal("uint", property.Type);
            Assert.False(property.HasSetMethod);
        }

        [Fact]
        public void PropertyTestNoSetterStringRep()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var propertySymbol = (IPropertySymbol)a.GetTypeByMetadataName("TestLibrary.PublicClass").GetMembers("propertyGet").Single();
            PropertyAPIV property = new PropertyAPIV(propertySymbol);

            Assert.Contains("public uint propertyGet { get; }", property.ToString());
        }

        [Fact]
        public void PropertyTestHasSetter()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var propertySymbol = (IPropertySymbol)a.GetTypeByMetadataName("TestLibrary.PublicClass").GetMembers("propertyBoth").Single();
            PropertyAPIV property = new PropertyAPIV(propertySymbol);
            
            Assert.Equal("propertyBoth", property.Name);
            Assert.Equal("int", property.Type);
            Assert.True(property.HasSetMethod);
        }

        [Fact]
        public void PropertyTestHasSetterStringRep()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var propertySymbol = (IPropertySymbol)a.GetTypeByMetadataName("TestLibrary.PublicClass").GetMembers("propertyBoth").Single();
            PropertyAPIV property = new PropertyAPIV(propertySymbol);

            Assert.Contains("public int propertyBoth { get; set; }", property.ToString());
        }
    }
}
