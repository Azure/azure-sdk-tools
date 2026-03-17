// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Benchmarks.Scenarios.Typespec;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;

/// <summary>
/// Loads AuthoringScenario instances from JSON files.
/// </summary>
public static class AuthoringScenarioLoader
{
    /// <summary>
    /// Loads all AuthoringScenario instances from JSON files in the TestData directory.
    /// </summary>
    /// <returns>An enumerable of AuthoringScenario instances.</returns>
    public static IEnumerable<AuthoringScenario> LoadFromJsonFiles(string? authoringSpecRepo = null, string? authoringSkillPath = null)
    {
        // Look for TestData in the source directory, not the build output
        var baseDir = AppContext.BaseDirectory;
        var testDataPath = Path.Combine(baseDir, "TestData", "TypeSpec");
        
        // If not found in build output, try to find it relative to the source
        if (!Directory.Exists(testDataPath))
        {
            // Navigate up from bin/Debug/net8.0 to project root
            var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            testDataPath = Path.Combine(projectRoot, "TestData", "TypeSpec");
        }
        
        if (!Directory.Exists(testDataPath))
        {
            Console.Error.WriteLine($"Warning: TestData directory not found at '{testDataPath}'");
            yield break;
        }

        var jsonFiles = Directory.GetFiles(testDataPath, "*.json", SearchOption.AllDirectories);

        foreach (var jsonFile in jsonFiles)
        {
            TestCaseCollection? testCases = null;
            try
            {
                var jsonContent = File.ReadAllText(jsonFile);
                testCases = JsonSerializer.Deserialize<TestCaseCollection>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Failed to parse JSON file '{jsonFile}': {ex.Message}");
                continue;
            }

            if (testCases?.TestCases == null)
            {
                continue;
            }

            foreach (var testCase in testCases.TestCases)
            {
                if (string.IsNullOrWhiteSpace(testCase.Name) || string.IsNullOrWhiteSpace(testCase.Prompt))
                {
                    Console.Error.WriteLine($"Warning: Skipping invalid test case in '{jsonFile}' (missing name or prompt)");
                    continue;
                }

                yield return new AuthoringScenario(
                    name: testCase.Name,
                    description: testCase.Description ?? string.Empty,
                    prompt: testCase.Prompt,
                    tspProjectPath: null,
                    testTspFiles: testCase.TestFiles,
                    toolsToCall: testCase.ToolsToCall,
                    verifyPlan: testCase.VerifyPlan ?? new List<string>(),
                    authoringSpecRepo: authoringSpecRepo,
                    authoringSkillPath: authoringSkillPath
                );
            }
        }
    }

    /// <summary>
    /// Represents the root JSON structure containing test cases.
    /// </summary>
    private class TestCaseCollection
    {
        [JsonPropertyName("testCases")]
        public List<TestCaseData>? TestCases { get; set; }
    }

    /// <summary>
    /// Represents a single test case from the JSON file.
    /// </summary>
    private class TestCaseData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;

        [JsonPropertyName("testfiles")]
        public List<string>? TestFiles { get; set; }

        [JsonPropertyName("toolsToCall")]
        public List<string>? ToolsToCall { get; set; }

        [JsonPropertyName("verifyPlan")]
        public List<string>? VerifyPlan { get; set; }

    }
}
