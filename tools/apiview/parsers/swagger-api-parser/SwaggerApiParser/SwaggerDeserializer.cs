using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace SwaggerApiParser
{
    public static class SwaggerDeserializer
    {
        public static async Task<SwaggerSpec> Deserialize(string swaggerFilePath)
        {
            var fullPath = Path.GetFullPath(swaggerFilePath);
            await using FileStream openStream = File.OpenRead(swaggerFilePath);
            SwaggerSpec swaggerSpec = await JsonSerializer.DeserializeAsync<SwaggerSpec>(openStream);
            return swaggerSpec;
        }
    }
}
