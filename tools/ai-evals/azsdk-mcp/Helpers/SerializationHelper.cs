using System.Text.Json;
using azsdk_mcp.Models;

namespace azsdk_mcp.Helpers
{
    public static class SerializationHelper
    {
        public static async Task<ScenarioData> LoadScenarioFromJsonAsync(string jsonPath)
        {
            var jsonContent = await File.ReadAllTextAsync(jsonPath);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var scenarioData = JsonSerializer.Deserialize<ScenarioData>(jsonContent, options);

            if (scenarioData == null)
            {
                throw new InvalidOperationException($"Failed to deserialize scenario data from {jsonPath}");
            }

            return scenarioData;
        }
    }
}
