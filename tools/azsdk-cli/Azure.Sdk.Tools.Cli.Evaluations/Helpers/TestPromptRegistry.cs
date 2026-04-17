using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Evaluations.Helpers
{
    /// <summary>
    /// Represents a single test prompt entry
    /// </summary>
    public record TestPromptEntry(
        [property: JsonPropertyName("toolName")] string ToolName,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("category")] string Category);

    /// <summary>
    /// JSON structure for TestPrompts.json
    /// </summary>
    internal class TestPromptsFile
    {
        [JsonPropertyName("prompts")]
        public List<TestPromptEntry> Prompts { get; set; } = [];
    }

    /// <summary>
    /// Loads and parses test prompts from TestPrompts.json.
    /// Provides methods to retrieve prompts for evaluation testing.
    /// </summary>
    public class TestPromptRegistry
    {
        private readonly List<TestPromptEntry> _prompts = [];

        /// <summary>
        /// All loaded test prompts
        /// </summary>
        public IReadOnlyList<TestPromptEntry> Prompts => _prompts.AsReadOnly();

        /// <summary>
        /// Load test prompts from the default TestPrompts.json file location
        /// </summary>
        public static async Task<TestPromptRegistry> LoadFromDefaultPathAsync()
        {
            // TestData folder is copied to output directory via csproj
            var assemblyLocation = typeof(TestPromptRegistry).Assembly.Location;
            var assemblyDir = Path.GetDirectoryName(assemblyLocation)!;
            var testPromptsPath = Path.Combine(assemblyDir, "TestData", "TestPrompts.json");

            if (File.Exists(testPromptsPath))
            {
                return await LoadFromJsonAsync(testPromptsPath);
            }

            throw new FileNotFoundException(
                $"TestPrompts.json not found at: {testPromptsPath}. Ensure TestData is copied to output directory.");
        }

        /// <summary>
        /// Load test prompts from a JSON file
        /// </summary>
        public static async Task<TestPromptRegistry> LoadFromJsonAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Test prompts file not found: {filePath}");
            }

            var registry = new TestPromptRegistry();
            await using var stream = File.OpenRead(filePath);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            var file = await JsonSerializer.DeserializeAsync<TestPromptsFile>(stream, options)
                ?? throw new InvalidOperationException($"Failed to deserialize {filePath}");

            registry._prompts.AddRange(file.Prompts);
            return registry;
        }

        /// <summary>
        /// Get all prompts as (ToolName, Prompt) tuples
        /// </summary>
        public IEnumerable<(string ToolName, string Prompt)> GetAllPrompts()
        {
            return _prompts.Select(p => (p.ToolName, p.Prompt));
        }

        /// <summary>
        /// Get all prompts for a specific tool
        /// </summary>
        public IEnumerable<string> GetPromptsForTool(string toolName)
        {
            return _prompts
                .Where(p => p.ToolName.Equals(toolName, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Prompt);
        }

        /// <summary>
        /// Get all prompts for a specific category (e.g., "all", "azure-rest-api-specs")
        /// </summary>
        public IEnumerable<TestPromptEntry> GetPromptsForCategory(string category)
        {
            if (category.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return _prompts;
            }

            return _prompts.Where(p =>
                p.Category.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                p.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get all unique tool names that have prompts
        /// </summary>
        public IEnumerable<string> GetToolsWithPrompts()
        {
            return _prompts.Select(p => p.ToolName).Distinct(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Find tools that exist in the available tools list but have no test prompts
        /// </summary>
        public IEnumerable<string> GetToolsWithoutPrompts(IEnumerable<string> allToolNames)
        {
            var toolsWithPrompts = GetToolsWithPrompts().ToHashSet(StringComparer.OrdinalIgnoreCase);
            return allToolNames.Where(t => !toolsWithPrompts.Contains(t));
        }

        /// <summary>
        /// Get count of prompts per tool
        /// </summary>
        public Dictionary<string, int> GetPromptCountsByTool()
        {
            return _prompts
                .GroupBy(p => p.ToolName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        }
    }
}
