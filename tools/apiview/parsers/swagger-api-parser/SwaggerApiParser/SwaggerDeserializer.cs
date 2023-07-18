using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using SwaggerApiParser.Specs;

namespace SwaggerApiParser 
{
    public static class SwaggerDeserializer
    {
        public static async Task<Swagger> Deserialize(string swaggerFilePath)
        {
            var fullPath = Path.GetFullPath(swaggerFilePath);
            await using FileStream openStream = File.OpenRead(swaggerFilePath);
            Swagger swaggerSpec = await JsonSerializer.DeserializeAsync<Swagger>(openStream);
            return swaggerSpec;
        }
    }
}

