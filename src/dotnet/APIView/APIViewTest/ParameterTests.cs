using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Linq;
using APIView;
using Xunit;

namespace APIViewTest
{
    public class ParameterTests
    {
        [Fact]
        public void ParameterTestNoRefKindStringDefaultValue()
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

            ImmutableArray<ParameterAPIV> parameters = method.Parameters;
            Assert.Equal(2, parameters.Length);

            ParameterAPIV param = null;
            ParameterAPIV num = null;
            foreach (ParameterAPIV p in parameters)
            {
                if (p.Name.Equals("param"))
                    param = p;
                else
                    num = p;
            }

            Assert.False(param == null || num == null);
            Assert.Equal("T", param.Type);
            Assert.Equal("param", param.Name);
            Assert.Null(param.ExplicitDefaultValue);

            Assert.Equal("string", num.Type);
            Assert.Equal("str", num.Name);
            Assert.Equal("hello", num.ExplicitDefaultValue);

            Assert.Contains("int TypeParamParamsMethod<T>(T param, string str = \"hello\");", method.ToString());
        }

        [Fact]
        public void ParameterTestSomeRefKindNoDefaultValue()
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
                if (m.Name.Equals("RefKindParamMethod"))
                    method = m;
            }

            ImmutableArray<ParameterAPIV> parameters = method.Parameters;
            Assert.Single(parameters);

            Assert.Equal("ref string", parameters[0].Type);
            Assert.Equal("str", parameters[0].Name);
            Assert.Null(parameters[0].ExplicitDefaultValue);

            Assert.Contains("string RefKindParamMethod(ref string str);", method.ToString());
        }
    }
}
