
using System.Linq;
using System.Threading.Tasks;
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
    }
}
