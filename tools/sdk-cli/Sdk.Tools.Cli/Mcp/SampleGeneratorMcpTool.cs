using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ModelContextProtocol.Server;
using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Models;
using Sdk.Tools.Cli.Services;
using Sdk.Tools.Cli.Services.Languages;
using Sdk.Tools.Cli.Services.Languages.Samples;

namespace Sdk.Tools.Cli.Mcp;

/// <summary>
/// MCP tool wrapper for the sample generator.
/// </summary>
[McpServerToolType]
public class SampleGeneratorMcpTool
{
    private readonly AiService _aiService;
    private readonly FileHelper _fileHelper;
    
    public SampleGeneratorMcpTool(
        AiService aiService,
        FileHelper fileHelper)
    {
        _aiService = aiService;
        _fileHelper = fileHelper;
    }
    
    [McpServerTool(Name = "generate_samples"), Description(
        "Generate code samples for an SDK package. " +
        "Analyzes the SDK source code and generates runnable example code demonstrating key features and usage patterns. " +
        "Supports .NET/C#, Python, Java, JavaScript, TypeScript, and Go SDKs. " +
        "Automatically detects the SDK language, locates source files, and creates samples in the appropriate output folder. " +
        "Use this tool when you need to create documentation examples, quickstart code, or usage demonstrations for an SDK.")]
    public async Task<string> GenerateSamplesAsync(
        [Description("Absolute path to the SDK package root directory. The tool will automatically detect source folders (src/, lib/, etc.) and sample output locations (samples/, examples/, etc.).")] string packagePath,
        [Description("Optional output directory for generated samples. If not specified, the tool auto-detects the appropriate folder (e.g., 'samples', 'examples') or creates one.")] string? outputPath = null,
        [Description("Optional custom prompt to guide sample generation. Examples: 'Generate samples for authentication scenarios', 'Create examples showing error handling', 'Focus on async/await patterns'.")] string? prompt = null,
        CancellationToken cancellationToken = default)
    {
        // Auto-detect source, samples folders, and language
        var sdkInfo = SdkInfo.Scan(packagePath);
        if (sdkInfo.Language == null)
        {
            return "Error: Could not detect package language.";
        }
        
        try
        {
            // Create language context for streaming
            var context = CreateLanguageContext(sdkInfo.Language.Value);
            var systemPrompt = BuildSystemPrompt(context);
            
            // Stream user prompt (prefix + context) directly to AI without materialization
            var userPromptStream = StreamUserPromptAsync(prompt, sdkInfo.SourceFolder, context, cancellationToken);
            
            // Stream parsed samples as they complete
            List<GeneratedSample> samples = [];
            await foreach (var sample in _aiService.StreamItemsAsync<GeneratedSample>(
                systemPrompt, userPromptStream, null, null, cancellationToken))
            {
                if (!string.IsNullOrEmpty(sample.Name) && !string.IsNullOrEmpty(sample.Code))
                {
                    samples.Add(sample);
                }
            }
            
            // Write to auto-detected or specified output folder
            var output = outputPath ?? sdkInfo.SuggestedSamplesFolder;
            Directory.CreateDirectory(output);
            
            foreach (var sample in samples)
            {
                var filePath = Path.Combine(output, sample.FileName ?? $"{sample.Name}{context.FileExtension}");
                await File.WriteAllTextAsync(filePath, sample.Code, cancellationToken);
            }
            
            return $"Generated {samples.Count} sample(s) in {output}";
        }
        catch (Exception ex)
        {
            return $"Error generating samples: {ex.Message}";
        }
    }
    
    // Language context factory for all supported languages
    private SampleLanguageContext CreateLanguageContext(SdkLanguage language) => language switch
    {
        SdkLanguage.DotNet => new DotNetSampleLanguageContext(_fileHelper),
        SdkLanguage.Python => new PythonSampleLanguageContext(_fileHelper),
        SdkLanguage.JavaScript => new JavaScriptSampleLanguageContext(_fileHelper),
        SdkLanguage.TypeScript => new TypeScriptSampleLanguageContext(_fileHelper),
        SdkLanguage.Java => new JavaSampleLanguageContext(_fileHelper),
        SdkLanguage.Go => new GoSampleLanguageContext(_fileHelper),
        _ => throw new NotSupportedException($"Language {language} not supported")
    };
    
    private static string BuildSystemPrompt(SampleLanguageContext context) =>
        $"Generate runnable SDK samples. {context.GetInstructions()}";
    
    private static string BuildUserPromptPrefix(string? customPrompt)
    {
        var prompt = customPrompt ?? "Generate samples demonstrating the main features of this SDK.";
        return $"{prompt}\n\nSource code context:\n";
    }
    
    /// <summary>
    /// Streams the complete user prompt including prefix and context.
    /// </summary>
    private async IAsyncEnumerable<string> StreamUserPromptAsync(
        string? customPrompt,
        string sourceFolder,
        SampleLanguageContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Yield the prompt prefix
        yield return BuildUserPromptPrefix(customPrompt);
        
        // Stream context (source code)
        await foreach (var chunk in context.StreamContextAsync(
            new[] { sourceFolder }, null, ct: cancellationToken))
        {
            yield return chunk;
        }
    }
}
