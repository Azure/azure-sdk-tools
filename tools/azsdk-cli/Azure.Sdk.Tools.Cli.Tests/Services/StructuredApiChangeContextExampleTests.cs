using System;
using System.ClientModel;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.ClientUpdate;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

public class TestClientUpdateLlmService : IClientUpdateLlmService
{
    private readonly ClientUpdateLlmService _realService;
    
    public TestClientUpdateLlmService()
    {
        // Create a real service that will use mock responses (no deployment configured)
        var mockClient = new AzureOpenAIClient(new Uri("https://test.openai.azure.com/"), new ApiKeyCredential("mock-key"));
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<ClientUpdateLlmService>();
        _realService = new ClientUpdateLlmService(mockClient, logger);
    }

    public async Task<(List<CustomizationImpact> impacts, List<PatchProposal> patches)> AnalyzeAndProposePatchesAsync(string fileContent, string fileName, StructuredApiChangeContext structuredChanges, CancellationToken ct)
    {
        return await _realService.AnalyzeAndProposePatchesAsync(fileContent, fileName, structuredChanges, ct);
    }



    public string BuildDependencyChainAnalysisPrompt(string fileContent, string fileName, StructuredApiChangeContext structuredChanges)
    {
        return _realService.BuildDependencyChainAnalysisPrompt(fileContent, fileName, structuredChanges);
    }

        public (List<CustomizationImpact> impacts, List<PatchProposal> patches) ParseCombinedLlmResponse(string llmResponse, string fileName, StructuredApiChangeContext structuredChanges)
        {
            return (new List<CustomizationImpact>(), new List<PatchProposal>());
        }
    }

[TestFixture]
public class StructuredApiChangeContextExampleTests
{
    [Test]
    public void ShowStructuredApiChangeContextExample()
    {
        // Arrange - Create a JavaUpdateLanguageService to access the parsing methods
        var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var logger = loggerFactory.CreateLogger<JavaUpdateLanguageService>();
        var service = new JavaUpdateLanguageService(null!, logger, new TestClientUpdateLlmService());
        
        // Load the real test data
        var testJsonPath = @"C:\Users\savaity\IdeaProjects\azure-sdk-tools\tools\azsdk-cli\Azure.Sdk.Tools.Cli.Tests\TestAssets\Customization\apiview-diff.json";
        var jsonContent = File.ReadAllText(testJsonPath);
        var doc = JsonDocument.Parse(jsonContent);
        
        var changes = new List<ApiChange>();
        if (doc.RootElement.TryGetProperty("changes", out var changesElement))
        {
            foreach (var changeElement in changesElement.EnumerateArray())
            {
                var change = ParseApiChangeFromJsonElement(changeElement);
                if (change != null)
                {
                    changes.Add(change);
                }
            }
        }

        // Use reflection to access the private PrepareStructuredApiChanges method
        var method = typeof(JavaUpdateLanguageService).GetMethod("PrepareStructuredApiChanges", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var context = (StructuredApiChangeContext)method!.Invoke(service, new object[] { changes })!;

        // Generate detailed output
        var outputPath = @"C:\Users\savaity\IdeaProjects\azure-sdk-tools\tools\azsdk-cli\Azure.Sdk.Tools.Cli.Tests\TestAssets\Customization\StructuredContext_ActualData.txt";
        
        var output = "=== ACTUAL STRUCTURED API CHANGE CONTEXT FROM TEST DATA ===\n\n";
        
        output += $"TOTAL CHANGES: {context.Changes.Count}\n\n";
        
        output += "=== CHANGES BY KIND ===\n";
        foreach (var kvp in context.ChangesByKind.OrderBy(x => x.Key))
        {
            output += $"{kvp.Key}: {kvp.Value.Count} changes\n";
            foreach (var change in kvp.Value.Take(2)) // Show first 2 of each kind
            {
                output += $"  - {change.Symbol}: {change.Detail.Substring(0, Math.Min(60, change.Detail.Length))}...\n";
            }
            output += "\n";
        }
        
        output += $"=== METHOD CHANGES: {context.MethodChanges.Count} ===\n";
        foreach (var change in context.MethodChanges.Take(5))
        {
            output += $"Kind: {change.Kind}\n";
            output += $"Symbol: {change.Symbol}\n";
            output += $"Detail: {change.Detail.Substring(0, Math.Min(100, change.Detail.Length))}...\n";
            if (change.Metadata.Any())
            {
                output += "Key Metadata:\n";
                foreach (var meta in change.Metadata.Take(3))
                {
                    output += $"  {meta.Key}: {meta.Value}\n";
                }
            }
            output += "\n";
        }
        
        output += $"=== PARAMETER CHANGES: {context.ParameterChanges.Count} ===\n";
        foreach (var change in context.ParameterChanges.Take(3))
        {
            output += $"Kind: {change.Kind}\n";
            output += $"Symbol: {change.Symbol}\n";
            output += $"Has paramNameChange: {change.Metadata.ContainsKey("paramNameChange")}\n";
            if (change.Metadata.ContainsKey("parameterNames"))
            {
                output += $"Parameter Names: {change.Metadata["parameterNames"]}\n";
            }
            output += "\n";
        }
        
        output += $"=== TYPE CHANGES: {context.TypeChanges.Count} ===\n";
        foreach (var change in context.TypeChanges)
        {
            output += $"Kind: {change.Kind}\n";
            output += $"Symbol: {change.Symbol}\n";
            output += $"Detail: {change.Detail}\n\n";
        }
        
        File.WriteAllText(outputPath, output);
        
        // Assert
        Assert.That(context.Changes.Count, Is.EqualTo(15), "Should have 15 total changes");
        Assert.That(context.MethodChanges.Count, Is.GreaterThan(10), "Should have multiple method changes");
        Assert.That(context.ParameterChanges.Count, Is.GreaterThan(5), "Should have multiple parameter changes");
        
        Console.WriteLine($"Generated detailed output at: {outputPath}");
        Console.WriteLine($"Total: {context.Changes.Count}, Methods: {context.MethodChanges.Count}, Params: {context.ParameterChanges.Count}, Types: {context.TypeChanges.Count}");
    }
    
    // Helper method to parse API changes (simplified version)
    private ApiChange? ParseApiChangeFromJsonElement(JsonElement changeElement)
    {
        var apiChange = new ApiChange();
        
        if (changeElement.TryGetProperty("changeType", out var kindElement))
        {
            apiChange.Kind = kindElement.GetString() ?? "";
        }
        
        // Extract symbol from metadata
        if (changeElement.TryGetProperty("meta", out var metaElement))
        {
            if (metaElement.TryGetProperty("methodName", out var methodNameElement))
            {
                apiChange.Symbol = methodNameElement.GetString() ?? "";
            }
            else if (metaElement.TryGetProperty("fqn", out var fqnElement))
            {
                var fqn = fqnElement.GetString() ?? "";
                var lastDotIndex = fqn.LastIndexOf('.');
                apiChange.Symbol = lastDotIndex >= 0 ? fqn.Substring(lastDotIndex + 1) : fqn;
            }
        }
        
        // Build detail
        if (changeElement.TryGetProperty("before", out var beforeElement) &&
            changeElement.TryGetProperty("after", out var afterElement))
        {
            apiChange.Detail = $"{beforeElement.GetString() ?? ""} -> {afterElement.GetString() ?? ""}";
        }
        else if (changeElement.TryGetProperty("after", out var afterOnlyElement))
        {
            apiChange.Detail = $"Added: {afterOnlyElement.GetString() ?? ""}";
        }
        else if (changeElement.TryGetProperty("before", out var beforeOnlyElement))
        {
            apiChange.Detail = $"Removed: {beforeOnlyElement.GetString() ?? ""}";
        }
        
        // Extract metadata
        apiChange.Metadata = new Dictionary<string, string>();
        if (changeElement.TryGetProperty("meta", out var metaElement2))
        {
            foreach (var prop in metaElement2.EnumerateObject())
            {
                var value = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString() ?? "",
                    JsonValueKind.Array => string.Join(",", prop.Value.EnumerateArray().Select(e => e.GetString() ?? "")),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => prop.Value.ToString()
                };
                
                if (!string.IsNullOrEmpty(value))
                {
                    apiChange.Metadata[prop.Name] = value;
                }
            }
        }
        
        return string.IsNullOrEmpty(apiChange.Kind) ? null : apiChange;
    }
}
