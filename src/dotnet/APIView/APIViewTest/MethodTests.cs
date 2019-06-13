using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Linq;
using APIView;
using Xunit;
using System.Reflection.Metadata;

namespace APIViewTest
{
    public class MethodTests
    {
        [Fact]
        public void MethodTestNoAttributesOneTypeParamMultipleParams()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var methodSymbol = (IMethodSymbol)a.GetTypeByMetadataName("TestLibrary.PublicInterface`1").GetMembers("TypeParamParamsMethod").Single();
            MethodAPIV method = new MethodAPIV(methodSymbol);

            Assert.False(method == null);
            Assert.True(method.IsInterfaceMethod);
            Assert.False(method.IsStatic);
            Assert.False(method.IsVirtual);
            Assert.False(method.IsSealed);
            Assert.False(method.IsOverride);
            Assert.True(method.IsAbstract);
            Assert.False(method.IsExtern);
            Assert.Equal("int", method.ReturnType);

            ImmutableArray<AttributeData> attributes = method.Attributes;
            Assert.Empty(attributes);

            ImmutableArray<ParameterAPIV> parameters = method.Parameters;
            Assert.Equal(2, parameters.Length);

            ImmutableArray<TypeParameterAPIV> typeParameters = method.TypeParameters;
            Assert.Single(typeParameters);
        }

        [Fact]
        public void MethodTestNoAttributesOneTypeParamMultipleParamsStringRep()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var methodSymbol = (IMethodSymbol)a.GetTypeByMetadataName("TestLibrary.PublicInterface`1").GetMembers("TypeParamParamsMethod").Single();
            MethodAPIV method = new MethodAPIV(methodSymbol);

            Assert.Contains("int TypeParamParamsMethod<T>(T param, string str = \"hello\");", method.ToString());
        }

        [Fact]
        public void MethodTestOneAttributeNoTypeParamsOneParam()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var methodSymbol = (IMethodSymbol)a.GetTypeByMetadataName("TestLibrary.PublicClass").GetMembers("StaticVoid").Single();
            MethodAPIV method = new MethodAPIV(methodSymbol);

            Assert.False(method == null);
            Assert.False(method.IsInterfaceMethod);
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

            ImmutableArray<TypeParameterAPIV> typeParameters = method.TypeParameters;
            Assert.Empty(typeParameters);
        }

        [Fact]
        public void MethodTestOneAttributeNoTypeParamsOneParamStringRep()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var methodSymbol = (IMethodSymbol)a.GetTypeByMetadataName("TestLibrary.PublicClass").GetMembers("StaticVoid").Single();
            MethodAPIV method = new MethodAPIV(methodSymbol);

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
