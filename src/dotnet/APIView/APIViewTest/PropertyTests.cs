using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
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
            Assert.NotNull(publicClass);
            Assert.Equal("PublicClass", publicClass.Name);

            ImmutableArray<PropertyAPIV> properties = publicClass.Properties;
            PropertyAPIV property = null;
            foreach (PropertyAPIV p in properties)
            {
                if (p.Name.Equals("propertyGet"))
                    property = p;
            }
            Assert.NotNull(property);
            Assert.Equal("propertyGet", property.Name);
            Assert.Equal("uint", property.Type);
            Assert.False(property.HasSetMethod);

            Assert.Contains("public uint propertyGet { get; }", property.ToString());
        }

        [Fact]
        public void PropertyTestHasSetter()
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
            Assert.NotNull(publicClass);
            Assert.Equal("PublicClass", publicClass.Name);

            ImmutableArray<PropertyAPIV> properties = publicClass.Properties;
            PropertyAPIV property = null;
            foreach (PropertyAPIV p in properties)
            {
                if (p.Name.Equals("propertyBoth"))
                    property = p;
            }
            Assert.NotNull(property);
            Assert.Equal("propertyBoth", property.Name);
            Assert.Equal("int", property.Type);
            Assert.True(property.HasSetMethod);

            Assert.Contains("public int propertyBoth { get; set; }", property.ToString());
        }
    }
}
