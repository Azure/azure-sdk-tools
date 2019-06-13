using Microsoft.CodeAnalysis;
using APIView;
using Xunit;

namespace APIViewTest
{
    public class NamedTypeTests
    {
        [Fact]
        public void NamedTypeTestClassSomeEventsSomeFieldsNoMethodsSomeNamedTypes()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.SomeEventsSomeFieldsNoMethodsSomeNamedTypes");
            NamedTypeAPIV publicClass = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Equal("SomeEventsSomeFieldsNoMethodsSomeNamedTypes", publicClass.Name);
            Assert.Equal("class", publicClass.Type);
            Assert.Single(publicClass.Events);
            Assert.Single(publicClass.Fields);
            Assert.Empty(publicClass.Methods);
            Assert.Single(publicClass.NamedTypes);
        }

        [Fact]
        public void NamedTypeTestClassSomeEventsSomeFieldsNoMethodsSomeNamedTypesStringRep()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.SomeEventsSomeFieldsNoMethodsSomeNamedTypes");
            NamedTypeAPIV publicClass = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Contains("public class SomeEventsSomeFieldsNoMethodsSomeNamedTypes {", publicClass.ToString());
        }

        [Fact]
        public void NamedTypeTestInterfaceNoEventsNoFieldsSomeMethodsNoNamedTypes()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.PublicInterface`1");
            NamedTypeAPIV publicInterface = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Equal("PublicInterface", publicInterface.Name);
            Assert.Equal("interface", publicInterface.Type);
            Assert.Empty(publicInterface.Events);
            Assert.Empty(publicInterface.Fields);
            Assert.Equal(2, publicInterface.Methods.Length);
            Assert.Empty(publicInterface.NamedTypes);
        }

        [Fact]
        public void NamedTypeTestInterfaceNoEventsNoFieldsSomeMethodsNoNamedTypesStringRep()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.PublicInterface`1");
            NamedTypeAPIV publicInterface = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Contains("public interface PublicInterface {", publicInterface.ToString());
        }

        [Fact]
        public void NamedTypeTestImplementsInterface()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.ImplementingClass");
            NamedTypeAPIV implementer = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Equal("ImplementingClass", implementer.Name);
            Assert.Equal("class", implementer.Type);
            Assert.Single(implementer.Implementations);
            Assert.Equal("TestLibrary.PublicInterface<int>", implementer.Implementations[0]);
        }

        [Fact]
        public void NamedTypeTestImplementsInterfaceStringRep()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.ImplementingClass");
            NamedTypeAPIV implementer = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Contains("public class ImplementingClass : TestLibrary.PublicInterface<int> {", implementer.ToString());
        }

        [Fact]
        public void NamedTypeEnumDefaultUnderlyingType()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.PublicClass", "PublicEnum");
            NamedTypeAPIV publicEnum = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Equal("PublicEnum", publicEnum.Name);
            Assert.Equal("enum", publicEnum.Type);
            Assert.Equal("int", publicEnum.EnumUnderlyingType);
        }
        
        [Fact]
        public void NamedTypeEnumDefaultUnderlyingTypeStringRep()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.PublicClass", "PublicEnum");
            NamedTypeAPIV publicEnum = new NamedTypeAPIV(namedTypeSymbol);

            string stringRep = publicEnum.ToString();
            Assert.Contains("public enum PublicEnumDefault {", stringRep);
            Assert.Contains("One = 0,", stringRep);
            Assert.Contains("Two = 1,", stringRep);
            Assert.Contains("Three = 2", stringRep);
        }
        
        [Fact]
        public void NamedTypeEnumDeclaredUnderlyingType()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.ImplementingClass", "PublicEnum");
            NamedTypeAPIV publicEnum = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Equal("PublicEnum", publicEnum.Name);
            Assert.Equal("enum", publicEnum.Type);
            Assert.Equal("long", publicEnum.EnumUnderlyingType);
        }

        [Fact]
        public void NamedTypeEnumDeclaredUnderlyingTypeStringRep()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.ImplementingClass", "PublicEnum");
            NamedTypeAPIV publicEnum = new NamedTypeAPIV(namedTypeSymbol);

            string stringRep = publicEnum.ToString();
            Assert.Contains("public enum PublicEnum : long {", stringRep);
            Assert.Contains("One = 1,", stringRep);
            Assert.Contains("Two = 2,", stringRep);
            Assert.Contains("Three = 3", stringRep);
        }

        [Fact]
        public void NamedTypeDelegate()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.publicDelegate");
            NamedTypeAPIV publicDelegate = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Equal("publicDelegate", publicDelegate.Name);
            Assert.Equal("delegate", publicDelegate.Type);
        }

        [Fact]
        public void NamedTypeDelegateStringRep()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.publicDelegate");
            NamedTypeAPIV publicDelegate = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Contains("public delegate int publicDelegate(int num = 10);", publicDelegate.ToString());
        }
    }
}
