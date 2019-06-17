using Microsoft.CodeAnalysis;
using APIView;
using Xunit;

namespace APIViewTest
{
    public class FieldTests
    {
        [Fact]
        public void FieldTestReadOnly()
        {
            var fieldSymbol = (IFieldSymbol)TestResource.GetTestMember("TestLibrary.PublicClass", "publicField");
            FieldAPIV field = new FieldAPIV(fieldSymbol);
            
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
            var fieldSymbol = (IFieldSymbol)TestResource.GetTestMember("TestLibrary.PublicClass", "publicField");
            FieldAPIV field = new FieldAPIV(fieldSymbol);

            Assert.Equal("public readonly int publicField;", field.ToString());
        }

        [Fact]
        public void FieldTestConstant()
        {
            var fieldSymbol = (IFieldSymbol)TestResource.GetTestMember("TestLibrary.PublicClass", "publicString");
            FieldAPIV field = new FieldAPIV(fieldSymbol);

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
            var fieldSymbol = (IFieldSymbol)TestResource.GetTestMember("TestLibrary.PublicClass", "publicString");
            FieldAPIV field = new FieldAPIV(fieldSymbol);

            Assert.Equal("public static const string publicString = \"constant string\";", field.ToString());
        }

        [Fact]
        public void FieldTestProtected()
        {
            var fieldSymbol = (IFieldSymbol)TestResource.GetTestMember("TestLibrary.PublicClass", "protectedField");
            FieldAPIV field = new FieldAPIV(fieldSymbol);

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
            var fieldSymbol = (IFieldSymbol)TestResource.GetTestMember("TestLibrary.PublicClass", "protectedField");
            FieldAPIV field = new FieldAPIV(fieldSymbol);

            Assert.Equal("protected int protectedField;", field.ToString());
        }
    }
}
