using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.Cli.Evaluations.Helpers
{
    /// <summary>
    /// Represents a single test prompt entry from TestPrompts.md
    /// </summary>
    public record TestPromptEntry(string ToolName, string Prompt, string Category);

    /// <summary>
    /// Loads and parses test prompts from TestPrompts.md markdown file.
    /// Provides methods to retrieve prompts for evaluation testing.
    /// </summary>
    public class TestPromptRegistry
    {
        private readonly List<TestPromptEntry> _prompts = [];
        private static readonly Regex TableRowRegex = new(@"^\|\s*(\S+)\s*\|\s*(.+?)\s*\|\s*(\S+)\s*\|$", RegexOptions.Compiled);

        /// <summary>
        /// All loaded test prompts
        /// </summary>
        public IReadOnlyList<TestPromptEntry> Prompts => _prompts.AsReadOnly();

        /// <summary>
        /// Load test prompts from the default TestPrompts.md file location
        /// </summary>
        public static async Task<TestPromptRegistry> LoadFromDefaultPathAsync()
        {
            // Find the TestPrompts.md file relative to the test assembly
            var assemblyLocation = typeof(TestPromptRegistry).Assembly.Location;
            var assemblyDir = Path.GetDirectoryName(assemblyLocation)!;
            
            // Navigate up to find the TestData folder
            // In build output: artifacts/bin/Azure.Sdk.Tools.Cli.Evaluations/Debug/net8.0/
            // TestData is at: tools/azsdk-cli/Azure.Sdk.Tools.Cli.Evaluations/TestData/
            
            // Try multiple possible locations
            string[] possiblePaths =
            [
                Path.Combine(assemblyDir, "TestData", "TestPrompts.md"),
                Path.Combine(assemblyDir, "..", "..", "..", "..", "..", "tools", "azsdk-cli", "Azure.Sdk.Tools.Cli.Evaluations", "TestData", "TestPrompts.md"),
                Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestPrompts.md"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "TestData", "TestPrompts.md"),
            ];

            foreach (var path in possiblePaths)
            {
                var normalizedPath = Path.GetFullPath(path);
                if (File.Exists(normalizedPath))
                {
                    return await LoadFromMarkdownAsync(normalizedPath);
                }
            }

            throw new FileNotFoundException(
                $"TestPrompts.md not found. Searched in: {string.Join(", ", possiblePaths.Select(Path.GetFullPath))}");
        }

        /// <summary>
        /// Load test prompts from a markdown file
        /// </summary>
        public static async Task<TestPromptRegistry> LoadFromMarkdownAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Test prompts file not found: {filePath}");
            }

            var registry = new TestPromptRegistry();
            var lines = await File.ReadAllLinesAsync(filePath);

            bool inTable = false;
            bool headerSkipped = false;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Detect table start (header row)
                if (trimmedLine.StartsWith("| Tool Name"))
                {
                    inTable = true;
                    headerSkipped = false;
                    continue;
                }

                // Skip separator row (|:---|:---|:---|)
                if (inTable && !headerSkipped && trimmedLine.StartsWith("|:") || trimmedLine.StartsWith("| :") || trimmedLine.StartsWith("|-"))
                {
                    headerSkipped = true;
                    continue;
                }

                // Detect table end (empty line or non-table content)
                if (inTable && (string.IsNullOrWhiteSpace(trimmedLine) || !trimmedLine.StartsWith("|")))
                {
                    inTable = false;
                    headerSkipped = false;
                    continue;
                }

                // Parse table row
                if (inTable && headerSkipped)
                {
                    var match = TableRowRegex.Match(trimmedLine);
                    if (match.Success)
                    {
                        var toolName = match.Groups[1].Value.Trim();
                        var prompt = match.Groups[2].Value.Trim();
                        var category = match.Groups[3].Value.Trim();

                        // Skip if this looks like a header or separator
                        if (toolName.Equals("Tool Name", StringComparison.OrdinalIgnoreCase) ||
                            toolName.StartsWith("-") || toolName.StartsWith(":"))
                        {
                            continue;
                        }

                        registry._prompts.Add(new TestPromptEntry(toolName, prompt, category));
                    }
                }
            }

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
