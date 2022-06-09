using System.Linq;
using System.Threading.Tasks;
using SwaggerApiParser;
using Xunit;
using Xunit.Abstractions;

namespace SwaggerApiParserTest;

public class SchemaCacheTest
{
    private readonly ITestOutputHelper output;

    public SchemaCacheTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task TestResolveSchema()
    {
        const string runCommandsFilePath = "./fixtures/runCommands.json";
        var swaggerSpec = await SwaggerDeserializer.Deserialize(runCommandsFilePath);

        SchemaCache cache = new SchemaCache();
        foreach (var schema in swaggerSpec.definitions)
        {
            cache.Cache.Add(schema.Key, schema.Value);
        }

        var resolvedSchema = cache.GetResolvedSchema(swaggerSpec.definitions.First().Value);
        this.output.WriteLine(resolvedSchema.ToString());

        swaggerSpec.definitions.TryGetValue("VirtualMachineRunCommandProperties", out var runCommandProperties);
        var resolvedRunCommandProperties = cache.GetResolvedSchema(runCommandProperties);
        this.output.WriteLine(resolvedRunCommandProperties.ToString());
    }
}
