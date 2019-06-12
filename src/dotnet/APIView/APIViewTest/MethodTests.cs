using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Linq;
using APIView;
using Xunit;

namespace APIViewTest
{
    public class MethodTests
    {
        [Fact]
        public void MethodTestNoAttributesOneTypeParamMultipleParams()
        {
            AssemblyAPIV assembly = AssemblyAPIV.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceAPIV globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<NamespaceAPIV> namespaces = globalNamespace.Namespaces;
            NamespaceAPIV testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedTypeAPIV> namedTypes = testLibNamespace.NamedTypes;
            NamedTypeAPIV publicInterface = null;
            foreach (NamedTypeAPIV NamedType in namedTypes)
            {
                if (NamedType.Name.Equals("PublicInterface"))
                    publicInterface = NamedType;
            }
            Assert.False(publicInterface == null);
            Assert.Equal("PublicInterface", publicInterface.Name);

            ImmutableArray<MethodAPIV> methods = publicInterface.Methods;
            MethodAPIV method = null;
            foreach (MethodAPIV m in methods)
            {
                if (m.Name.Equals("TypeParamParamsMethod"))
                    method = m;
            }

            Assert.False(method == null);
            Assert.False(method.IsStatic);
            Assert.False(method.IsVirtual);
            Assert.False(method.IsSealed);
            Assert.False(method.IsOverride);
            Assert.True(method.IsAbstract);
            Assert.False(method.IsExtern);
            Assert.Equal("int", method.ReturnType);

            ImmutableArray<TypeParameterAPIV> typeParameters = method.TypeParameters;
            Assert.Single(typeParameters);
            Assert.Equal("T", typeParameters[0].Name);

            Assert.Contains("int TypeParamParamsMethod<T>(T param, string str = \"hello\");", method.ToString());
        }

        [Fact]
        public void MethodTestOneAttributeNoTypeParamsOneParam()
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

            ImmutableArray<MethodAPIV> methods = publicClass.Methods;
            MethodAPIV method = null;
            foreach (MethodAPIV m in methods)
            {
                if (m.Name.Equals("StaticVoid"))
                    method = m;
            }

            Assert.False(method == null);
            Assert.True(method.IsStatic);
            Assert.False(method.IsVirtual);
            Assert.False(method.IsSealed);
            Assert.False(method.IsOverride);
            Assert.False(method.IsAbstract);
            Assert.False(method.IsExtern);
            Assert.Equal("void", method.ReturnType);

            ImmutableArray<AttributeData> attributes = method.Attributes;
            Assert.Single(attributes);

            ImmutableArray<ParameterAPIV> parameters = method.Parameters;
            Assert.Single(parameters);
            Assert.Equal("args", parameters[0].Name);
            Assert.Equal("string[]", parameters[0].Type);

            string stringRep = method.ToString();
            Assert.Contains("[Conditional(\"DEBUG\")]", stringRep);
            Assert.Contains("public static void StaticVoid(string[] args) {", stringRep);
        }

        [Fact]
        public void MethodTestMultipleAttributesMultipleTypeParamsNoParams()
        {
            //TODO
        }
    }
}
