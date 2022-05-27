using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ApiView;
using swagger_api_parser;
using Xunit;
using Xunit.Abstractions;

namespace SwaggerApiParserTest;

public class SwaggerApiViewGeneratorTest
{
    private readonly ITestOutputHelper output;

    public SwaggerApiViewGeneratorTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task TestGenerateSwaggerApiViewRunCommands()
    {
        const string runCommandsFilePath = "./fixtures/runCommands.json";
        var swaggerSpec = await SwaggerDeserializer.Deserialize(runCommandsFilePath);
        var apiViewGenerator = new SwaggerApiViewGenerator();
        var apiView = SwaggerApiViewGenerator.GenerateSwaggerApiView(swaggerSpec);

        Assert.Equal("2.0", apiView.General.swagger);
        Assert.Equal("VirtualMachineRunCommands", apiView.Paths.First().Key);

        var option = new JsonSerializerOptions() {DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull};
        var jsonDoc = JsonSerializer.SerializeToDocument(apiView, option);

        var ret = Visitor.GenerateCodeFileTokens(jsonDoc);
        var outputFilePath = Path.GetFullPath("./part_output.json");

        CodeFile codeFile = new CodeFile()
        {
            Tokens = ret,
            Language = "Swagger",
            VersionString = "0",
            Name = "tmp",
            PackageName = "tmp",
            Navigation = apiView.BuildNavigationItems()
        };
        this.output.WriteLine($"Write result to: {outputFilePath}");
        await using FileStream writer = File.Open(outputFilePath, FileMode.Create);
        await codeFile.SerializeAsync(writer);
    }
}
