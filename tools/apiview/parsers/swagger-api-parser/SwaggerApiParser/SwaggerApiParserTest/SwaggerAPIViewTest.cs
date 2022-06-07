using System;
using System.IO;
using System.Threading.Tasks;
using SwaggerApiParser;
using Xunit;
using Xunit.Abstractions;

namespace SwaggerApiParserTest;

public class SwaggerApiViewTest
{
    private readonly ITestOutputHelper output;

    public SwaggerApiViewTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task TestComputeOneFile()
    {
        const string runCommandFilePath = "./fixtures/runCommands.json";
        var swaggerSpec = await SwaggerDeserializer.Deserialize(runCommandFilePath);

        SwaggerApiViewRoot root = new SwaggerApiViewRoot("Microsoft.Compute", "Microsoft.Compute");
        root.AddSwaggerSpec(swaggerSpec, Path.GetFileName(runCommandFilePath), "Microsoft.Compute");

        var codeFile = root.GenerateCodeFile();
        var outputFilePath = Path.GetFullPath("./compute_root_one_file_codefile.json");
        
        this.output.WriteLine($"Write output to: {outputFilePath}");
        await using FileStream writer = File.Open(outputFilePath, FileMode.Create);
        await codeFile.SerializeAsync(writer);
    }
    
    [Fact]
    public async Task TestComputeTwoFiles()
    {
        const string runCommandFilePath = "./fixtures/runCommands.json";
        var runCommandsSwaggerSpec = await SwaggerDeserializer.Deserialize(runCommandFilePath);

        const String computeFilePath = "./fixtures/compute.json";
        var computeSwaggerSpec = await SwaggerDeserializer.Deserialize(computeFilePath);

        SwaggerApiViewRoot root = new SwaggerApiViewRoot("Microsoft.Compute", "Microsoft.Compute");
        root.AddSwaggerSpec(runCommandsSwaggerSpec, Path.GetFileName(runCommandFilePath), "Microsoft.Compute");
        root.AddSwaggerSpec(computeSwaggerSpec, Path.GetFileName(computeFilePath), "Microsoft.Compute");
        

        var codeFile = root.GenerateCodeFile();
        var outputFilePath = Path.GetFullPath("./compute_root_two_file_codefile.json");
        
        this.output.WriteLine($"Write output to: {outputFilePath}");
        await using FileStream writer = File.Open(outputFilePath, FileMode.Create);
        await codeFile.SerializeAsync(writer);
    }
}
