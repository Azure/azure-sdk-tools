using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using APIView;
using Xunit;

namespace APIViewTest
{
    public class FieldTests
    {
        [Fact]
        public void FieldTestReadOnly()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var fieldSymbol = (IFieldSymbol)a.GetTypeByMetadataName("TestLibrary.PublicClass").GetMembers("publicField").Single();
            FieldAPIV field = new FieldAPIV(fieldSymbol);
            
            Assert.NotNull(field);
            Assert.Equal("publicField", field.Name);
            Assert.Equal("int", field.Type);
            Assert.Equal("public", field.Accessibility);
            Assert.False(field.IsConstant);
            Assert.True(field.IsReadOnly);
            Assert.False(field.IsStatic);
            Assert.False(field.IsVolatile);
            Assert.Null(field.Value);
        }

        [Fact]
        public void FieldTestReadOnlyStringRep()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var fieldSymbol = (IFieldSymbol)a.GetTypeByMetadataName("TestLibrary.PublicClass").GetMembers("publicField").Single();
            FieldAPIV field = new FieldAPIV(fieldSymbol);

            Assert.Contains("public readonly int publicField;", field.ToString());
        }

        [Fact]
        public void FieldTestConstant()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var fieldSymbol = (IFieldSymbol)a.GetTypeByMetadataName("TestLibrary.PublicClass").GetMembers("publicString").Single();
            FieldAPIV field = new FieldAPIV(fieldSymbol);

            Assert.NotNull(field);
            Assert.Equal("publicString", field.Name);
            Assert.Equal("string", field.Type);
            Assert.Equal("public", field.Accessibility);
            Assert.True(field.IsConstant);
            Assert.False(field.IsReadOnly);
            Assert.True(field.IsStatic);
            Assert.False(field.IsVolatile);
            Assert.Equal("constant string", field.Value);
        }

        [Fact]
        public void FieldTestConstantStringRep()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var fieldSymbol = (IFieldSymbol)a.GetTypeByMetadataName("TestLibrary.PublicClass").GetMembers("publicString").Single();
            FieldAPIV field = new FieldAPIV(fieldSymbol);

            Assert.Contains("public static const string publicString = \"constant string\";", field.ToString());
        }

        [Fact]
        public void FieldTestProtected()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var fieldSymbol = (IFieldSymbol)a.GetTypeByMetadataName("TestLibrary.PublicClass").GetMembers("protectedField").Single();
            FieldAPIV field = new FieldAPIV(fieldSymbol);

            Assert.NotNull(field);
            Assert.Equal("protectedField", field.Name);
            Assert.Equal("int", field.Type);
            Assert.Equal("protected", field.Accessibility);
            Assert.False(field.IsConstant);
            Assert.False(field.IsReadOnly);
            Assert.False(field.IsStatic);
            Assert.False(field.IsVolatile);
            Assert.Null(field.Value);
        }

        [Fact]
        public void FieldTestProtectedStringRep()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var fieldSymbol = (IFieldSymbol)a.GetTypeByMetadataName("TestLibrary.PublicClass").GetMembers("protectedField").Single();
            FieldAPIV field = new FieldAPIV(fieldSymbol);

            Assert.Contains("protected int protectedField;", field.ToString());
        }
    }
}
