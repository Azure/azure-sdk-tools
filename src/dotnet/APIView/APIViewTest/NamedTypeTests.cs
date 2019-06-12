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
            AssemblyAPIV assembly = AssemblyAPIV.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceAPIV globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<NamespaceAPIV> namespaces = globalNamespace.Namespaces;
            NamespaceAPIV testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedTypeAPIV> NamedTypes = testLibNamespace.NamedTypes;
            NamedTypeAPIV publicClass = null;

            foreach (NamedTypeAPIV NamedType in NamedTypes)
            {
                if (NamedType.Name.Equals("SomeEventsSomeFieldsNoMethodsSomeNamedTypes"))
                    publicClass = NamedType;
            }
            Assert.False(publicClass == null);
            Assert.Equal("SomeEventsSomeFieldsNoMethodsSomeNamedTypes", publicClass.Name);
            Assert.Equal("class", publicClass.Type);

            ImmutableArray<EventAPIV> events = publicClass.Events;
            Assert.Single(events);

            ImmutableArray<FieldAPIV> fields = publicClass.Fields;
            Assert.Single(fields);

            ImmutableArray<MethodAPIV> methods = publicClass.Methods;
            Assert.Empty(methods);

            NamedTypes = publicClass.NamedTypes;
            Assert.Single(NamedTypes);

            Assert.Contains("public class SomeEventsSomeFieldsNoMethodsSomeNamedTypes {", publicClass.ToString());
        }

        [Fact]
        public void NamedTypeTestInterfaceNoEventsNoFieldsSomeMethodsNoNamedTypes()
        {
            AssemblyAPIV assembly = AssemblyAPIV.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceAPIV globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<NamespaceAPIV> namespaces = globalNamespace.Namespaces;
            NamespaceAPIV testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedTypeAPIV> NamedTypes = testLibNamespace.NamedTypes;
            NamedTypeAPIV publicInterface = null;

            foreach (NamedTypeAPIV NamedType in NamedTypes)
            {
                if (NamedType.Name.Equals("PublicInterface"))
                    publicInterface = NamedType;
            }
            Assert.False(publicInterface == null);
            Assert.Equal("PublicInterface", publicInterface.Name);
            Assert.Equal("interface", publicInterface.Type);

            ImmutableArray<EventAPIV> events = publicInterface.Events;
            Assert.Empty(events);

            ImmutableArray<FieldAPIV> fields = publicInterface.Fields;
            Assert.Empty(fields);

            ImmutableArray<MethodAPIV> methods = publicInterface.Methods;
            Assert.Equal(2, methods.Length);

            NamedTypes = publicInterface.NamedTypes;
            Assert.Empty(NamedTypes);

            Assert.Contains("public interface PublicInterface {", publicInterface.ToString());
        }

        [Fact]
        public void NamedTypeTestImplementsInterface()
        {
            AssemblyAPIV assembly = AssemblyAPIV.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceAPIV globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<NamespaceAPIV> namespaces = globalNamespace.Namespaces;
            NamespaceAPIV testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedTypeAPIV> NamedTypes = testLibNamespace.NamedTypes;
            NamedTypeAPIV implementer = null;

            foreach (NamedTypeAPIV NamedType in NamedTypes)
            {
                if (NamedType.Name.Equals("ImplementingClass"))
                    implementer = NamedType;
            }
            Assert.False(implementer == null);
            Assert.Equal("ImplementingClass", implementer.Name);
            Assert.Equal("class", implementer.Type);

            ImmutableArray<string> implementations = implementer.Implementations;
            Assert.Single(implementations);
            Assert.Equal("PublicInterface<int>", implementations[0]);

            Assert.Contains("public class ImplementingClass : PublicInterface<int> {", implementer.ToString());
        }

        [Fact]
        public void NamedTypeEnumDefaultUnderlyingType()
        {
            AssemblyAPIV assembly = AssemblyAPIV.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceAPIV globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<NamespaceAPIV> namespaces = globalNamespace.Namespaces;
            NamespaceAPIV testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedTypeAPIV> NamedTypes = testLibNamespace.NamedTypes;
            NamedTypeAPIV publicClass = null;

            foreach (NamedTypeAPIV NamedType in NamedTypes)
            {
                if (NamedType.Name.Equals("PublicClass"))
                    publicClass = NamedType;
            }
            Assert.False(publicClass == null);
            Assert.Equal("PublicClass", publicClass.Name);

            NamedTypes = publicClass.NamedTypes;
            NamedTypeAPIV publicEnum = null;

            foreach (NamedTypeAPIV NamedType in NamedTypes)
            {
                if (NamedType.Name.Equals("PublicEnum"))
                    publicEnum = NamedType;
            }
            Assert.False(publicEnum == null);
            Assert.Equal("PublicEnum", publicEnum.Name);
            Assert.Equal("enum", publicEnum.Type);
            Assert.Equal("int", publicEnum.EnumUnderlyingType);

            string stringRep = publicEnum.ToString();
            Assert.Contains("public enum PublicEnum {", stringRep);
            Assert.Contains("One = 0,", stringRep);
            Assert.Contains("Two = 1,", stringRep);
            Assert.Contains("Three = 2", stringRep);
        }

        [Fact]
        public void NamedTypeEnumDeclaredUnderlyingType()
        {
            AssemblyAPIV assembly = AssemblyAPIV.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceAPIV globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<NamespaceAPIV> namespaces = globalNamespace.Namespaces;
            NamespaceAPIV testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedTypeAPIV> NamedTypes = testLibNamespace.NamedTypes;
            NamedTypeAPIV publicClass = null;

            foreach (NamedTypeAPIV NamedType in NamedTypes)
            {
                if (NamedType.Name.Equals("PublicClass"))
                    publicClass = NamedType;
            }
            Assert.False(publicClass == null);
            Assert.Equal("PublicClass", publicClass.Name);

            NamedTypes = publicClass.NamedTypes;
            NamedTypeAPIV publicEnum = null;

            foreach (NamedTypeAPIV NamedType in NamedTypes)
            {
                if (NamedType.Name.Equals("PublicEnum"))
                    publicEnum = NamedType;
            }
            Assert.False(publicEnum == null);
            Assert.Equal("PublicEnum", publicEnum.Name);
            Assert.Equal("enum", publicEnum.Type);
            Assert.Equal("long", publicEnum.EnumUnderlyingType);

            string stringRep = publicEnum.ToString();
            Assert.Contains("public enum PublicEnum : long {", stringRep);
            Assert.Contains("One = 1,", stringRep);
            Assert.Contains("Two = 2,", stringRep);
            Assert.Contains("Three = 3", stringRep);
        }

        [Fact]
        public void NamedTypeDelegate()
        {
            AssemblyAPIV assembly = AssemblyAPIV.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceAPIV globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<NamespaceAPIV> namespaces = globalNamespace.Namespaces;
            NamespaceAPIV testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedTypeAPIV> NamedTypes = testLibNamespace.NamedTypes;
            NamedTypeAPIV publicDelegate = null;

            foreach (NamedTypeAPIV NamedType in NamedTypes)
            {
                if (NamedType.Name.Equals("publicDelegate"))
                    publicDelegate = NamedType;
            }
            Assert.False(publicDelegate == null);
            Assert.Equal("publicDelegate", publicDelegate.Name);
            Assert.Equal("delegate", publicDelegate.Type);

            Assert.Contains("public delegate int publicDelegate(int num = 10);", publicDelegate.ToString());
        }
    }
}
