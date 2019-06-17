using Microsoft.CodeAnalysis;
using APIView;
using Xunit;
using System;

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
            Assert.Equal(2, publicClass.Events.Length);
            Assert.Equal(2, publicClass.Fields.Length);
            Assert.Empty(publicClass.Methods);
            Assert.Equal(2, publicClass.NamedTypes.Length);
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
            Assert.Equal(3, publicInterface.Methods.Length);
            Assert.Empty(publicInterface.NamedTypes);
        }

        [Fact]
        public void NamedTypeTestInterfaceNoEventsNoFieldsSomeMethodsNoNamedTypesStringRep()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.PublicInterface`1");
            NamedTypeAPIV publicInterface = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Contains("public interface PublicInterface<T> {", publicInterface.ToString());
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
        public void NamedTypeTestEnumDefaultUnderlyingType()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.PublicClass", "PublicEnum");
            NamedTypeAPIV publicEnum = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Equal("PublicEnum", publicEnum.Name);
            Assert.Equal("enum", publicEnum.Type);
            Assert.Equal("int", publicEnum.EnumUnderlyingType);
        }
        
        [Fact]
        public void NamedTypeTestEnumDefaultUnderlyingTypeStringRep()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.PublicClass", "PublicEnum");
            NamedTypeAPIV publicEnum = new NamedTypeAPIV(namedTypeSymbol);

            string stringRep = publicEnum.ToString().Replace(Environment.NewLine, "");
            Assert.Equal("public enum PublicEnum {    One = 0,    Two = 1,    Three = 2,}", stringRep);
        }
        
        [Fact]
        public void NamedTypeTestEnumDeclaredUnderlyingType()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.ImplementingClass", "PublicEnum");
            NamedTypeAPIV publicEnum = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Equal("PublicEnum", publicEnum.Name);
            Assert.Equal("enum", publicEnum.Type);
            Assert.Equal("long", publicEnum.EnumUnderlyingType);
        }

        [Fact]
        public void NamedTypeTestEnumDeclaredUnderlyingTypeStringRep()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.ImplementingClass", "PublicEnum");
            NamedTypeAPIV publicEnum = new NamedTypeAPIV(namedTypeSymbol);

            string stringRep = publicEnum.ToString().Replace(Environment.NewLine, "");
            Assert.Equal("public enum PublicEnum : long {    One = 1,    Two = 2,    Three = 3,}", stringRep);
        }

        [Fact]
        public void NamedTypeTestDelegate()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.publicDelegate");
            NamedTypeAPIV publicDelegate = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Equal("publicDelegate", publicDelegate.Name);
            Assert.Equal("delegate", publicDelegate.Type);
        }

        [Fact]
        public void NamedTypeTestDelegateStringRep()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.publicDelegate");
            NamedTypeAPIV publicDelegate = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Equal("public delegate int publicDelegate(int num = 10) { }", publicDelegate.ToString());
        }

        [Fact]
        public void NamedTypeTestAutomaticConstructor()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.SomeEventsSomeFieldsNoMethodsSomeNamedTypes");
            NamedTypeAPIV publicClass = new NamedTypeAPIV(namedTypeSymbol);

            foreach (MethodAPIV method in publicClass.Methods)
            {
                Assert.NotEqual("SomeEventsSomeFieldsNoMethodsSomeNamedTypes", method.Name);
                Assert.NotEqual(".ctor", method.Name);
            }
        }

        [Fact]
        public void NamedTypeTestExplicitConstructor()
        {
            var namedTypeSymbol = (INamedTypeSymbol)TestResource.GetTestMember("TestLibrary.PublicClass");
            NamedTypeAPIV publicClass = new NamedTypeAPIV(namedTypeSymbol);

            bool constructorFound = false;
            foreach (MethodAPIV method in publicClass.Methods)
            {
                if (method.Name.Equals("PublicClass"))
                    constructorFound = true;
            }

            Assert.True(constructorFound);
        }
    }
}
