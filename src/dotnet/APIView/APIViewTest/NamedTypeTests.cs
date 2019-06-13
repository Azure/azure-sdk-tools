using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Linq;
using APIView;
using Xunit;

namespace APIViewTest
{
    public class NamedTypeTests
    {
        [Fact]
        public void NamedTypeTestClassSomeEventsSomeFieldsNoMethodsSomeNamedTypes()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var namedTypeSymbol = a.GetTypeByMetadataName("TestLibrary.SomeEventsSomeFieldsNoMethodsSomeNamedTypes");
            NamedTypeAPIV publicClass = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Equal("SomeEventsSomeFieldsNoMethodsSomeNamedTypes", publicClass.Name);
            Assert.Equal("class", publicClass.Type);

            ImmutableArray<EventAPIV> events = publicClass.Events;
            Assert.Single(events);

            ImmutableArray<FieldAPIV> fields = publicClass.Fields;
            Assert.Single(fields);

            ImmutableArray<MethodAPIV> methods = publicClass.Methods;
            Assert.Empty(methods);

            ImmutableArray<NamedTypeAPIV> namedTypes = publicClass.NamedTypes;
            Assert.Single(namedTypes);
        }

        [Fact]
        public void NamedTypeTestClassSomeEventsSomeFieldsNoMethodsSomeNamedTypesStringRep()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var namedTypeSymbol = a.GetTypeByMetadataName("TestLibrary.SomeEventsSomeFieldsNoMethodsSomeNamedTypes");
            NamedTypeAPIV publicClass = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Contains("public class SomeEventsSomeFieldsNoMethodsSomeNamedTypes {", publicClass.ToString());
        }

        [Fact]
        public void NamedTypeTestInterfaceNoEventsNoFieldsSomeMethodsNoNamedTypes()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var namedTypeSymbol = a.GetTypeByMetadataName("TestLibrary.PublicInterface`1");
            NamedTypeAPIV publicInterface = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Equal("PublicInterface", publicInterface.Name);
            Assert.Equal("interface", publicInterface.Type);

            ImmutableArray<EventAPIV> events = publicInterface.Events;
            Assert.Empty(events);

            ImmutableArray<FieldAPIV> fields = publicInterface.Fields;
            Assert.Empty(fields);

            ImmutableArray<MethodAPIV> methods = publicInterface.Methods;
            Assert.Equal(2, methods.Length);

            ImmutableArray<NamedTypeAPIV> namedTypes = publicInterface.NamedTypes;
            Assert.Empty(namedTypes);
        }

        [Fact]
        public void NamedTypeTestInterfaceNoEventsNoFieldsSomeMethodsNoNamedTypesStringRep()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var namedTypeSymbol = a.GetTypeByMetadataName("TestLibrary.PublicInterface`1");
            NamedTypeAPIV publicInterface = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Contains("public interface PublicInterface {", publicInterface.ToString());
        }

        [Fact]
        public void NamedTypeTestImplementsInterface()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var namedTypeSymbol = a.GetTypeByMetadataName("TestLibrary.ImplementingClass");
            NamedTypeAPIV implementer = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Equal("ImplementingClass", implementer.Name);
            Assert.Equal("class", implementer.Type);

            ImmutableArray<string> implementations = implementer.Implementations;
            Assert.Single(implementations);
            Assert.Equal("TestLibrary.PublicInterface<int>", implementations[0]);
        }

        [Fact]
        public void NamedTypeTestImplementsInterfaceStringRep()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var namedTypeSymbol = a.GetTypeByMetadataName("TestLibrary.ImplementingClass");
            NamedTypeAPIV implementer = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Contains("public class ImplementingClass : TestLibrary.PublicInterface<int> {", implementer.ToString());
        }

        [Fact]
        public void NamedTypeEnumDefaultUnderlyingType()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var namedTypeSymbol = (INamedTypeSymbol)a.GetTypeByMetadataName("TestLibrary.PublicClass").GetMembers("PublicEnum").Single();
            NamedTypeAPIV publicEnum = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Equal("PublicEnum", publicEnum.Name);
            Assert.Equal("enum", publicEnum.Type);
            Assert.Equal("int", publicEnum.EnumUnderlyingType);
        }
        
        [Fact]
        public void NamedTypeEnumDefaultUnderlyingTypeStringRep()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var namedTypeSymbol = (INamedTypeSymbol)a.GetTypeByMetadataName("TestLibrary.PublicClass").GetMembers("PublicEnum").Single();
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
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var namedTypeSymbol = (INamedTypeSymbol)a.GetTypeByMetadataName("TestLibrary.ImplementingClass").GetMembers("PublicEnum").Single();
            NamedTypeAPIV publicEnum = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Equal("PublicEnum", publicEnum.Name);
            Assert.Equal("enum", publicEnum.Type);
            Assert.Equal("long", publicEnum.EnumUnderlyingType);
        }

        [Fact]
        public void NamedTypeEnumDeclaredUnderlyingTypeStringRep()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var namedTypeSymbol = (INamedTypeSymbol)a.GetTypeByMetadataName("TestLibrary.ImplementingClass").GetMembers("PublicEnum").Single();
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
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var namedTypeSymbol = a.GetTypeByMetadataName("TestLibrary.publicDelegate");
            NamedTypeAPIV publicDelegate = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Equal("publicDelegate", publicDelegate.Name);
            Assert.Equal("delegate", publicDelegate.Type);
        }

        [Fact]
        public void NamedTypeDelegateStringRep()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var namedTypeSymbol = a.GetTypeByMetadataName("TestLibrary.publicDelegate");
            NamedTypeAPIV publicDelegate = new NamedTypeAPIV(namedTypeSymbol);

            Assert.Contains("public delegate int publicDelegate(int num = 10);", publicDelegate.ToString());
        }
    }
}
