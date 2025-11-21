using APIView.Model.V2;
using NSwag;
using SwaggerTreeStyleParser;

namespace SwaggerTreeStyleParserTests
{
    public class CodeFileBuilderTests
    {
        private CodeFileBuilder CreateBuilder() => new CodeFileBuilder();
        public static IEnumerable<object[]> NullBuildActions()
        {
            yield return new object[] { "BuildOpenApiInfoObject", (Action<CodeFileBuilder, List<ReviewLine>>)((b, list) => b.BuildOpenApiInfoObject(null, "Info", list)) };
            yield return new object[] { "BuildOpenApiContactObject", (Action<CodeFileBuilder, List<ReviewLine>>)((b, list) => b.BuildOpenApiContactObject(null, "Contact", list)) };
            yield return new object[] { "BuildOpenApiLicenseObject", (Action<CodeFileBuilder, List<ReviewLine>>)((b, list) => b.BuildOpenApiLicenseObject(null, "License", list)) };
            yield return new object[] { "BuildOpenApiSecuritySchemeObject", (Action<CodeFileBuilder, List<ReviewLine>>)((b, list) => b.BuildOpenApiSecuritySchemeObject(null, "SecurityDefinitions", list)) };
            yield return new object[] { "BuildOpenApiSecurityRequirementObject", (Action<CodeFileBuilder, List<ReviewLine>>)((b, list) => b.BuildOpenApiSecurityRequirementObject(null, "Security", list)) };
            yield return new object[] { "BuildOpenApiPathItemObject", (Action<CodeFileBuilder, List<ReviewLine>>)((b, list) => b.BuildOpenApiPathItemObject(null, "Paths", list)) };
            yield return new object[] { "BuildJsonSchemaDefinitions", (Action<CodeFileBuilder, List<ReviewLine>>)((b, list) => b.BuildJsonSchemaDefinitions(null, "Definitions", list)) };
            yield return new object[] { "BuildOpenApiExternalDocumentation", (Action<CodeFileBuilder, List<ReviewLine>>)((b, list) => b.BuildOpenApiExternalDocumentation(null, list)) };
            yield return new object[] { "BuildOpenApiParameterObject(dict)", (Action<CodeFileBuilder, List<ReviewLine>>)((b, list) => b.BuildOpenApiParameterObject((IDictionary<string, OpenApiParameter>)null, "Parameters", list)) };
            yield return new object[] { "BuildOpenApiParameterObject(list)", (Action<CodeFileBuilder, List<ReviewLine>>)((b, list) => b.BuildOpenApiParameterObject((IList<OpenApiParameter>)null, list)) };
            yield return new object[] { "BuildOpenApiResponseObject", (Action<CodeFileBuilder, List<ReviewLine>>)((b, list) => b.BuildOpenApiResponseObject(null, list)) };
        }


        [Theory]
        [MemberData(nameof(NullBuildActions))]
        public void Builder_Method_Skips_When_Object_Null(string name, Action<CodeFileBuilder, List<ReviewLine>> act)
        {
            var builder = new CodeFileBuilder();
            var collection = new List<ReviewLine>();

            act(builder, collection);

            Assert.Empty(collection);
        }

        [Fact]
        public void Build_Throws_On_Null_Document()
        {
            var builder = new CodeFileBuilder();
            Assert.Throws<ArgumentNullException>(() => builder.Build(null));
        }       
    }
}
