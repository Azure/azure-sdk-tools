using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SwaggerApiParser;
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
        SchemaCache schemaCache = new SchemaCache();
        var apiView = SwaggerApiViewGenerator.GenerateSwaggerApiView(swaggerSpec, "runCommands.json", schemaCache, "Microsoft.Compute");

        Assert.Equal("2.0", apiView.SwaggerApiViewGeneral.swagger);
        Assert.Equal("VirtualMachineRunCommands", apiView.Paths.First().Key);


        var codeFile = apiView.GenerateCodeFile();
        var outputFilePath = Path.GetFullPath("./runCommands_output.json");

        this.output.WriteLine($"Write result to: {outputFilePath}");
        await using FileStream writer = File.Open(outputFilePath, FileMode.Create);
        await codeFile.SerializeAsync(writer);
    }

    [Fact]
    public async Task TestGenerateSwaggerApiViewCompute()
    {
        const string runCommandsFilePath = "./fixtures/compute.json";
        var swaggerSpec = await SwaggerDeserializer.Deserialize(runCommandsFilePath);
        var apiViewGenerator = new SwaggerApiViewGenerator();
        SchemaCache schemaCache = new SchemaCache();
        var apiView = SwaggerApiViewGenerator.GenerateSwaggerApiView(swaggerSpec, "compute.json", schemaCache, "Microsoft.Compute");

        Assert.Equal("2.0", apiView.SwaggerApiViewGeneral.swagger);


        var codeFile = apiView.GenerateCodeFile();
        var outputFilePath = Path.GetFullPath("./compute_output.json");

        this.output.WriteLine($"Write result to: {outputFilePath}");
        await using FileStream writer = File.Open(outputFilePath, FileMode.Create);
        await codeFile.SerializeAsync(writer);
    }
}
