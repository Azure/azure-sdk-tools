using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Linq;
using APIView;
using Xunit;

namespace APIViewTest
{
    public class FieldTests
    {
        [Fact]
        public void FieldTestVariable()
        {
            AssemblyAPIV assembly = AssemblyAPIV.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceAPIV globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<NamespaceAPIV> namespaces = globalNamespace.Namespaces;
            NamespaceAPIV testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedTypeAPIV> classes = testLibNamespace.NamedTypes;
            NamedTypeAPIV publicClass = null;
            foreach (NamedTypeAPIV n in classes)
            {
                if (n.Name.Equals("PublicClass"))
                    publicClass = n;
            }
            Assert.NotNull(publicClass);
            Assert.Equal("PublicClass", publicClass.Name);

            ImmutableArray<FieldAPIV> fields = publicClass.Fields;
            Assert.NotEmpty(fields);

            FieldAPIV field = null;
            foreach (FieldAPIV f in fields)
            {
                if (f.Name.Equals("publicField"))
                    field = f;
            }
            Assert.NotNull(field);
            Assert.Equal("publicField", field.Name);
            Assert.Equal("int", field.Type);
            Assert.False(field.IsConstant);

            Assert.Contains("public int publicField;", field.ToString());
        }

        [Fact]
        public void FieldTestConstant()
        {
            AssemblyAPIV assembly = AssemblyAPIV.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceAPIV globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<NamespaceAPIV> namespaces = globalNamespace.Namespaces;
            NamespaceAPIV testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedTypeAPIV> classes = testLibNamespace.NamedTypes;
            NamedTypeAPIV publicClass = null;
            foreach (NamedTypeAPIV n in classes)
            {
                if (n.Name.Equals("PublicClass"))
                    publicClass = n;
            }
            Assert.NotNull(publicClass);
            Assert.Equal("PublicClass", publicClass.Name);

            ImmutableArray<FieldAPIV> fields = publicClass.Fields;
            Assert.NotEmpty(fields);

            FieldAPIV field = null;
            foreach (FieldAPIV f in fields)
            {
                if (f.Name.Equals("publicString"))
                    field = f;
            }
            Assert.NotNull(field);
            Assert.Equal("publicString", field.Name);
            Assert.Equal("string", field.Type);
            Assert.True(field.IsConstant);
            Assert.False(field.IsReadOnly);
            Assert.False(field.IsStatic);
            Assert.False(field.IsVolatile);
            Assert.Equal("constant string", field.Value);

            Assert.Contains("public const string publicString = \"constant string\";", field.ToString());
        }

        [Fact]
        public void FieldTestNonConstantDeclaredValue()
        {
            AssemblyAPIV assembly = AssemblyAPIV.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceAPIV globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<NamespaceAPIV> namespaces = globalNamespace.Namespaces;
            NamespaceAPIV testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedTypeAPIV> classes = testLibNamespace.NamedTypes;
            NamedTypeAPIV publicClass = null;
            foreach (NamedTypeAPIV n in classes)
            {
                if (n.Name.Equals("PublicClass"))
                    publicClass = n;
            }
            Assert.NotNull(publicClass);
            Assert.Equal("PublicClass", publicClass.Name);

            ImmutableArray<FieldAPIV> fields = publicClass.Fields;
            Assert.NotEmpty(fields);

            FieldAPIV field = null;
            foreach (FieldAPIV f in fields)
            {
                if (f.Name.Equals("publicField"))
                    field = f;
            }
            Assert.NotNull(field);
            Assert.Equal("publicField", field.Name);
            Assert.Equal("int", field.Type);
            Assert.False(field.IsConstant);
            Assert.True(field.IsReadOnly);
            Assert.False(field.IsStatic);
            Assert.False(field.IsVolatile);

            Assert.Contains("public readonly int publicField;", field.ToString());
        }
    }
}
