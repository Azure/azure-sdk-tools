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
        root.AddSwaggerSpec(swaggerSpec, Path.GetFullPath(runCommandFilePath), "Microsoft.Compute");

        var codeFile = root.GenerateCodeFile();
        var outputFilePath = Path.GetFullPath("./compute_root_one_file_codefile.json");

        this.output.WriteLine($"Write output to: {outputFilePath}");
        await using FileStream writer = File.Open(outputFilePath, FileMode.Create);
        await codeFile.SerializeAsync(writer);
    }

    [Fact]
    public async Task TestMediaComposition()
    {
        const string runCommandFilePath = "./fixtures/mediacomposition.json";
        var swaggerSpec = await SwaggerDeserializer.Deserialize(runCommandFilePath);

        SwaggerApiViewRoot root = new SwaggerApiViewRoot("Microsoft.Media", "Microsoft.Media");
        root.AddSwaggerSpec(swaggerSpec, Path.GetFullPath(runCommandFilePath), "Microsoft.Media");

        var codeFile = root.GenerateCodeFile();
        var outputFilePath = Path.GetFullPath("./media_codefile.json");

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
        root.AddSwaggerSpec(runCommandsSwaggerSpec, Path.GetFullPath(runCommandFilePath), "Microsoft.Compute");
        root.AddSwaggerSpec(computeSwaggerSpec, Path.GetFullPath(computeFilePath), "Microsoft.Compute");


        var codeFile = root.GenerateCodeFile();
        var outputFilePath = Path.GetFullPath("./compute_root_two_file_codefile.json");

        this.output.WriteLine($"Write output to: {outputFilePath}");
        await using FileStream writer = File.Open(outputFilePath, FileMode.Create);
        await codeFile.SerializeAsync(writer);
    }

    [Fact]
    public async Task TestPetStore()
    {
        const string petStoreFilePath = "./fixtures/petstore.json";
        var petStoreSwaggerSpec = await SwaggerDeserializer.Deserialize(petStoreFilePath);

        SwaggerApiViewRoot root = new SwaggerApiViewRoot("Microsoft.PetStore", "Microsoft.PetStore");
        root.AddSwaggerSpec(petStoreSwaggerSpec, Path.GetFullPath(petStoreFilePath), "Microsoft.PetStore");

        var codeFile = root.GenerateCodeFile();
        var outputFilePath = Path.GetFullPath("./petstore_codefile.json");
        this.output.WriteLine($"Write output to: {outputFilePath}");
        await using FileStream writer = File.Open(outputFilePath, FileMode.Create);
        await codeFile.SerializeAsync(writer);
    }
    
    [Fact]
    public async Task TestDeviceUpdate()
    {
        const string deviceUpdatePath = "./fixtures/deviceupdate.json";
        var deviceUpdateSwagger = await SwaggerDeserializer.Deserialize(deviceUpdatePath);

        SwaggerApiViewRoot root = new SwaggerApiViewRoot("Microsoft.DeviceUpdate", "Microsoft.DeviceUpdate");
        root.AddSwaggerSpec(deviceUpdateSwagger, Path.GetFullPath(deviceUpdatePath), "Microsoft.DeviceUpdate");

        var codeFile = root.GenerateCodeFile();
        var outputFilePath = Path.GetFullPath("./deviceupdate_codefile.json");
        this.output.WriteLine($"Write output to: {outputFilePath}");
        await using FileStream writer = File.Open(outputFilePath, FileMode.Create);
        await codeFile.SerializeAsync(writer);
    }
    
    [Fact]
    public async Task TestDeviceUpdateSmall()
    {
        const string deviceUpdatePath = "./fixtures/deviceupdatesmall.json";
        var deviceUpdateSwagger = await SwaggerDeserializer.Deserialize(deviceUpdatePath);

        SwaggerApiViewRoot root = new SwaggerApiViewRoot("Microsoft.DeviceUpdate", "Microsoft.DeviceUpdate");
        root.AddSwaggerSpec(deviceUpdateSwagger, Path.GetFullPath(deviceUpdatePath), "Microsoft.DeviceUpdate");

        var codeFile = root.GenerateCodeFile();
        var outputFilePath = Path.GetFullPath("./deviceupdatesmall_codefile.json");
        this.output.WriteLine($"Write output to: {outputFilePath}");
        await using FileStream writer = File.Open(outputFilePath, FileMode.Create);
        await codeFile.SerializeAsync(writer);
    }
    
    [Fact]
    public async Task TestContentModerator()
    {
        const string contentModerator = "./fixtures/ContentModerator.json";
        var contentModeratorSwagger = await SwaggerDeserializer.Deserialize(contentModerator);

        SwaggerApiViewRoot root = new SwaggerApiViewRoot("Microsoft.ContentModerator", "Microsoft.ContentModerator");
        root.AddSwaggerSpec(contentModeratorSwagger, Path.GetFullPath(contentModerator), "Microsoft.ContentModerator");

        var codeFile = root.GenerateCodeFile();
        var outputFilePath = Path.GetFullPath("./contentModerator_codefile.json");
        this.output.WriteLine($"Write output to: {outputFilePath}");
        await using FileStream writer = File.Open(outputFilePath, FileMode.Create);
        await codeFile.SerializeAsync(writer);
    }

    [Fact]
    public async Task TestMultivariate()
    {
        const string multiVariateSwaggerFile = "./fixtures/multivariate.json";
        var multiVariateSwagger = await SwaggerDeserializer.Deserialize(multiVariateSwaggerFile);

        SwaggerApiViewRoot root = new SwaggerApiViewRoot("Microsoft.CognitiveService", "Microsoft.CognitiveService");
        root.AddSwaggerSpec(multiVariateSwagger, Path.GetFullPath(multiVariateSwaggerFile), "Microsoft.CognitiveService");

        var codeFile = root.GenerateCodeFile();
        var outputFilePath = Path.GetFullPath("./multivariate_codefile.json");
        this.output.WriteLine($"Write output to: {outputFilePath}");
        await using FileStream writer = File.Open(outputFilePath, FileMode.Create);
        await codeFile.SerializeAsync(writer);
    }

    [Fact]
    public async Task TestCommunicate()
    {
        const string multiVariateSwaggerFile = "./fixtures/communicate.json";
        var multiVariateSwagger = await SwaggerDeserializer.Deserialize(multiVariateSwaggerFile);

        SwaggerApiViewRoot root = new SwaggerApiViewRoot("Microsoft.Communicate", "Microsoft.Communicate");
        root.AddSwaggerSpec(multiVariateSwagger, Path.GetFullPath(multiVariateSwaggerFile), "Microsoft.Communicate");

        var codeFile = root.GenerateCodeFile();
        var outputFilePath = Path.GetFullPath("./communicate_codefile.json");
        this.output.WriteLine($"Write output to: {outputFilePath}");
        await using FileStream writer = File.Open(outputFilePath, FileMode.Create);
        await codeFile.SerializeAsync(writer);
    }

    [Fact]
    public async Task TestSignalRCrossFileReferenceCommonTypes()
    {
        const string signalRFilePath = "./fixtures/signalr/resource-manager/Microsoft.SignalRService/stable/2022-02-01/signalr.json";
        var signalRSwagger = await SwaggerDeserializer.Deserialize(signalRFilePath);

        const string commonTypeFilePath = "./fixtures/common-types/resource-management/v2/types.json";
        var commonTypeSwagger = await SwaggerDeserializer.Deserialize(commonTypeFilePath);

        SwaggerApiViewRoot root = new SwaggerApiViewRoot("Microsoft.SignalR", "Microsoft.SignalR");
        root.AddSwaggerSpec(commonTypeSwagger, Path.GetFullPath(commonTypeFilePath), "Microsoft.SignalR");
        root.AddSwaggerSpec(signalRSwagger, Path.GetFullPath(signalRFilePath), "Microsoft.SignalR");

        var codeFile = root.GenerateCodeFile();

        var outputFilePath = Path.GetFullPath("./signalR_codefile.json");
        this.output.WriteLine($"Write output to: {outputFilePath}");
        await using FileStream writer = File.Open(outputFilePath, FileMode.Create);
        await codeFile.SerializeAsync(writer);
    }
}
