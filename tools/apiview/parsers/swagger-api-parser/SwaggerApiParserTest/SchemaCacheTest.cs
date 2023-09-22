using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SwaggerApiParser;
using SwaggerApiParser.Specs;
using Xunit;
using Xunit.Abstractions;

namespace SwaggerApiParserTest
{
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
            var runCommandsFilePath = "./fixtures/runCommands.json";
            var swaggerSpec = await SwaggerDeserializer.Deserialize(runCommandsFilePath);

            SchemaCache cache = new SchemaCache();
            foreach (var schema in swaggerSpec.definitions)
            {
                cache.AddSchema(runCommandsFilePath, schema.Key, schema.Value);
            }

            //var resolvedSchema = cache.GetResolvedSchema(swaggerSpec.definitions.First().Value,  runCommandsFilePath);
            // this.output.WriteLine(resolvedSchema.ToString());

            //swaggerSpec.definitions.TryGetValue("VirtualMachineRunCommandProperties", out var runCommandProperties);
            //var resolvedRunCommandProperties = cache.GetResolvedSchema(runCommandProperties,  runCommandsFilePath);
            //this.output.WriteLine(resolvedRunCommandProperties.ToString());
        }

        [Fact]
        public async Task TestResolveErrorDetail()
        {
            var dataPlaneFilePath = Path.GetFullPath("./fixtures/dataPlaneTypes.json");
            var swaggerSpec = await SwaggerDeserializer.Deserialize(dataPlaneFilePath);
            SchemaCache cache = new SchemaCache();
            foreach (var schema in swaggerSpec.definitions)
            {
                cache.AddSchema(dataPlaneFilePath, schema.Key, schema.Value);
            }

            LinkedList<string> refChain = new LinkedList<string>();
            // Add root level schema ref to refChain to detect whether the properties inside that level are reference to itself.
            refChain.AddFirst("#/definitions/ErrorDetail");

            Assert.Equal("ErrorDetail", swaggerSpec.definitions.First().Key);

            var errorDetail = cache.GetResolvedSchema(swaggerSpec.definitions.First().Value, dataPlaneFilePath, refChain);

            errorDetail.properties.TryGetValue("details", out var details);

            // The details property is an array of ErrorDetail(Circular reference).
            // Schema cache will only show ref of the array property without resolving. 
            Assert.Equal("#/definitions/ErrorDetail", details?.items.@ref);
            Assert.Null(details?.items.properties);

        }

    }
}
