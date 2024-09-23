using System;
using System.IO;
using System.Threading.Tasks;
using SwaggerApiParser;
using SwaggerApiParser.SwaggerApiView;
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
        await root.AddSwaggerSpec(swaggerSpec, Path.GetFullPath(runCommandFilePath), "Microsoft.Compute");

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
        await root.AddSwaggerSpec(swaggerSpec, Path.GetFullPath(runCommandFilePath), "Microsoft.Media");

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
        await root.AddSwaggerSpec(runCommandsSwaggerSpec, Path.GetFullPath(runCommandFilePath), "Microsoft.Compute");
        await root.AddSwaggerSpec(computeSwaggerSpec, Path.GetFullPath(computeFilePath), "Microsoft.Compute");


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
        await root.AddSwaggerSpec(petStoreSwaggerSpec, Path.GetFullPath(petStoreFilePath), "Microsoft.PetStore");

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
        await root.AddSwaggerSpec(deviceUpdateSwagger, Path.GetFullPath(deviceUpdatePath), "Microsoft.DeviceUpdate");

        var codeFile = root.GenerateCodeFile();
        var outputFilePath = Path.GetFullPath("./deviceupdate_codefile.json");
        this.output.WriteLine($"Write output to: {outputFilePath}");
        await using FileStream writer = File.Open(outputFilePath, FileMode.Create);
        await codeFile.SerializeAsync(writer);
    }

    [Fact]
    public async Task TestService()
    {
        const string deviceUpdatePath = "./fixtures/service.json";
        var serviceSwagger = await SwaggerDeserializer.Deserialize(deviceUpdatePath);

        SwaggerApiViewRoot root = new SwaggerApiViewRoot("Microsoft.Service", "Microsoft.Service");
        await root.AddSwaggerSpec(serviceSwagger, Path.GetFullPath(deviceUpdatePath), "Microsoft.Service");

        var codeFile = root.GenerateCodeFile();
        var outputFilePath = Path.GetFullPath("./service_codefile.json");
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
        await root.AddSwaggerSpec(deviceUpdateSwagger, Path.GetFullPath(deviceUpdatePath), "Microsoft.DeviceUpdate");

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
        await root.AddSwaggerSpec(contentModeratorSwagger, Path.GetFullPath(contentModerator), "Microsoft.ContentModerator");

        var codeFile = root.GenerateCodeFile();
        var outputFilePath = Path.GetFullPath("./contentModerator_codefile.json");
        this.output.WriteLine($"Write output to: {outputFilePath}");
        await using FileStream writer = File.Open(outputFilePath, FileMode.Create);
        await codeFile.SerializeAsync(writer);
    }
    
    [Fact]
    public async Task TestAzureOpenai()
    {
        const string openai = "./fixtures/azureopenai.json";
        var openaiSwagger = await SwaggerDeserializer.Deserialize(openai);

        SwaggerApiViewRoot root = new SwaggerApiViewRoot("Microsoft.OpenAI", "Microsoft.OpenAI");
        await root.AddSwaggerSpec(openaiSwagger, Path.GetFullPath(openai), "Microsoft.OpenAI");

        var codeFile = root.GenerateCodeFile();
        var outputFilePath = Path.GetFullPath("./openai_codefile.json");
        this.output.WriteLine($"Write output to: {outputFilePath}");
        await using FileStream writer = File.Open(outputFilePath, FileMode.Create);
        await codeFile.SerializeAsync(writer);
    }
    
    [Fact]
    public async Task TestPersonalize()
    {
        const string personal = "./fixtures/personalizer.json";
        var personalizeSwagger = await SwaggerDeserializer.Deserialize(personal);

        SwaggerApiViewRoot root = new SwaggerApiViewRoot("Microsoft.Personalize", "Microsoft.Personalize");
        await root.AddSwaggerSpec(personalizeSwagger, Path.GetFullPath(personal), "Microsoft.Personalize");

        var codeFile = root.GenerateCodeFile();
        var outputFilePath = Path.GetFullPath("./personal_codefile.json");
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
        await root.AddSwaggerSpec(multiVariateSwagger, Path.GetFullPath(multiVariateSwaggerFile), "Microsoft.CognitiveService");

        var codeFile = root.GenerateCodeFile();
        var outputFilePath = Path.GetFullPath("./multivariate_codefile.json");
        this.output.WriteLine($"Write output to: {outputFilePath}");
        await using FileStream writer = File.Open(outputFilePath, FileMode.Create);
        await codeFile.SerializeAsync(writer);
    }

    [Fact(Skip ="Missing test file due to recursive file search")]
    public async Task TestCommunicate()
    {
        const string multiVariateSwaggerFile = "./fixtures/communicate.json";
        var multiVariateSwagger = await SwaggerDeserializer.Deserialize(multiVariateSwaggerFile);

        SwaggerApiViewRoot root = new SwaggerApiViewRoot("Microsoft.Communicate", "Microsoft.Communicate");
        await root.AddSwaggerSpec(multiVariateSwagger, Path.GetFullPath(multiVariateSwaggerFile), "Microsoft.Communicate");

        var codeFile = root.GenerateCodeFile();
        var outputFilePath = Path.GetFullPath("./communicate_codefile.json");
        this.output.WriteLine($"Write output to: {outputFilePath}");
        await using FileStream writer = File.Open(outputFilePath, FileMode.Create);
        await codeFile.SerializeAsync(writer);
    }

    [Fact]
    public async Task TestDevCenterEnvironment()
    {
        const string devCenterSwaggerFile = "./fixtures/environment.json";
        var devCenter = await SwaggerDeserializer.Deserialize(devCenterSwaggerFile);

        SwaggerApiViewRoot root = new SwaggerApiViewRoot("Microsoft.DevCenter", "Microsoft.DevCenter");
        await root.AddSwaggerSpec(devCenter, Path.GetFullPath(devCenterSwaggerFile), "Microsoft.DevCenter");

        var codeFile = root.GenerateCodeFile();
        var outputFilePath = Path.GetFullPath("./devCenter_codefile.json");
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
        await root.AddSwaggerSpec(commonTypeSwagger, Path.GetFullPath(commonTypeFilePath), "Microsoft.SignalR");
        await root.AddSwaggerSpec(signalRSwagger, Path.GetFullPath(signalRFilePath), "Microsoft.SignalR");

        var codeFile = root.GenerateCodeFile();

        var outputFilePath = Path.GetFullPath("./signalR_codefile.json");
        this.output.WriteLine($"Write output to: {outputFilePath}");
        await using FileStream writer = File.Open(outputFilePath, FileMode.Create);
        await codeFile.SerializeAsync(writer);
    }
    

    [Fact]
    public async Task TestCommunicationEmailWithHeaderParameters()
    {   
        const string swaggerFilePath = "./fixtures/communicationEmail/spec/communicationEmail.json";
        var swaggerSpec = await SwaggerDeserializer.Deserialize(swaggerFilePath);
        
        const string commonTypeFilePath = "./fixtures/communicationEmail/common/stable/common.json";

        var commonSpec = await SwaggerDeserializer.Deserialize(commonTypeFilePath);

        SwaggerApiViewRoot root = new SwaggerApiViewRoot("Microsoft.Communication", "Microsoft.Communication");
        root.AddDefinitionToCache(commonSpec, commonTypeFilePath);
        await root.AddSwaggerSpec(commonSpec, commonTypeFilePath);
        await root.AddSwaggerSpec(swaggerSpec, Path.GetFullPath(swaggerFilePath), "Microsoft.Communication");

        var codeFile = root.GenerateCodeFile();
        var outputFilePath = Path.GetFullPath("./communication_codefile.json");
        this.output.WriteLine($"Write output to: {outputFilePath}");
        await using FileStream writer = File.Open(outputFilePath, FileMode.Create);
        await codeFile.SerializeAsync(writer);
        
    }

    [Fact]
    public async Task TestCodeFilePackageVersion()
    {
        const string runCommandFilePath = "./fixtures/runCommands.json";
        var swaggerSpec = await SwaggerDeserializer.Deserialize(runCommandFilePath);

        SwaggerApiViewRoot root = new SwaggerApiViewRoot("Microsoft.Compute", "Microsoft.Compute");
        await root.AddSwaggerSpec(swaggerSpec, Path.GetFullPath(runCommandFilePath), "Microsoft.Compute");

        var codeFile = root.GenerateCodeFile();
        Assert.Equal("2021-11-01", codeFile.PackageVersion);
    }
}
