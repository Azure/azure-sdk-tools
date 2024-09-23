using System.Threading.Tasks;
using SwaggerApiParser;
using Xunit.Abstractions;
using Xunit;

namespace SwaggerApiParserTest;

public class SwaggerDeserializerTest
{
    private readonly ITestOutputHelper output;

    public SwaggerDeserializerTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task TestDeserializePetStore()
    {
        const string petStoreFilePath = "./fixtures/petstore.json";
        var swaggerSpec = await SwaggerDeserializer.Deserialize(petStoreFilePath);
        Assert.Equal("2.0", swaggerSpec.swagger);
    }

    [Fact]
    public async Task TestDeserializeComputeRunCommands()
    {
        const string runCommandFilePath = "./fixtures/runCommands.json";
        var swaggerSpec = await SwaggerDeserializer.Deserialize(runCommandFilePath);
        Assert.Equal("2.0", swaggerSpec.swagger);
        Assert.Equal(8, swaggerSpec.paths.Count);

    }
}
