using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Linq;
using APIView;
using Xunit;

namespace APIViewTest
{
    public class TypeParameterTests
    {
        [Fact]
        public void TypeParameterTestCreation()
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

            ImmutableArray<MethodAPIV> methods = publicInterface.Methods;
            MethodAPIV method = null;
            foreach (MethodAPIV m in methods)
            {
                if (m.Name.Equals("TypeParamParamsMethod"))
                    method = m;
            }

            ImmutableArray<TypeParameterAPIV> parameters = method.TypeParameters;
            Assert.Single(parameters);
            Assert.Equal("T", parameters[0].Name);
        }
    }
}
