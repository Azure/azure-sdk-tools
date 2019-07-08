using Microsoft.CodeAnalysis;
using APIView;
using Xunit;

namespace APIViewTest
{
    public class PropertyTests
    {
        [Fact]
        public void PropertyTestNoSetter()
        {
            var propertySymbol = (IPropertySymbol)TestResource.GetTestMember("TestLibrary.PublicClass", "propertyGet");
            PropertyAPIV property = new PropertyAPIV(propertySymbol);
            
            Assert.Equal("propertyGet", property.Name);
            Assert.Equal("uint", property.Type);
            Assert.False(property.HasSetMethod);
        }

        [Fact]
        public void PropertyTestNoSetterStringRep()
        {
            var propertySymbol = (IPropertySymbol)TestResource.GetTestMember("TestLibrary.PublicClass", "propertyGet");
            PropertyAPIV property = new PropertyAPIV(propertySymbol);

            Assert.Equal("public uint propertyGet { get; }", property.ToString());
        }

        [Fact]
        public void PropertyTestHasSetter()
        {
            var propertySymbol = (IPropertySymbol)TestResource.GetTestMember("TestLibrary.PublicClass", "propertyBoth");
            PropertyAPIV property = new PropertyAPIV(propertySymbol);
            
            Assert.Equal("propertyBoth", property.Name);
            Assert.Equal("int", property.Type);
            Assert.True(property.HasSetMethod);
        }

        [Fact]
        public void PropertyTestHasSetterStringRep()
        {
            var propertySymbol = (IPropertySymbol)TestResource.GetTestMember("TestLibrary.PublicClass", "propertyBoth");
            PropertyAPIV property = new PropertyAPIV(propertySymbol);

            Assert.Equal("public int propertyBoth { get; set; }", property.ToString());
        }
    }
}
