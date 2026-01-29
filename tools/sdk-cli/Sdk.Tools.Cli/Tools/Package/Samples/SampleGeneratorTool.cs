// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Models;
using Sdk.Tools.Cli.Services;
using Sdk.Tools.Cli.Services.Languages;
using Sdk.Tools.Cli.Services.Languages.Samples;

namespace Sdk.Tools.Cli.Tools.Package.Samples;

public class SampleGeneratorTool
{
    private readonly AiService _aiService;
    private readonly FileHelper _fileHelper;
    private readonly ConfigurationHelper _configHelper;
    private readonly ILogger<SampleGeneratorTool> _logger;
    
    public SampleGeneratorTool(
        AiService aiService,
        FileHelper fileHelper,
        ConfigurationHelper configHelper,
        ILogger<SampleGeneratorTool> logger)
    {
        _aiService = aiService;
        _fileHelper = fileHelper;
        _configHelper = configHelper;
        _logger = logger;
    }
    
    public async Task<int> ExecuteAsync(
        string sdkPath,
        string? outputPath,
        string? language,
        string? prompt,
        int? count,
        string? model,
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        sdkPath = Path.GetFullPath(sdkPath);
        if (!Directory.Exists(sdkPath))
        {
            ConsoleUx.Error($"SDK path does not exist: {sdkPath}");
            return 1;
        }
        
        Console.WriteLine();
        
        // Auto-detect source and samples folders with spinner
        var sdkInfo = await ConsoleUx.SpinnerAsync("Scanning SDK...", async () =>
        {
            await Task.Yield(); // Allow spinner to start
            return SdkInfo.Scan(sdkPath);
        }, cancellationToken);
        
        var samplesFolder = outputPath ?? sdkInfo.SuggestedSamplesFolder;
        var existingSamplesPath = Directory.Exists(samplesFolder) ? samplesFolder : null;
        var existingCount = existingSamplesPath is not null 
            ? Directory.EnumerateFiles(samplesFolder, "*" + (sdkInfo.FileExtension ?? ".*"), SearchOption.AllDirectories).Count() 
            : 0;
        
        ConsoleUx.Info($"Detected {ConsoleUx.Bold(sdkInfo.LanguageName ?? "unknown")} SDK");
        ConsoleUx.Info($"Source: {sdkInfo.SourceFolder}");
        if (existingCount > 0)
            ConsoleUx.Info($"Existing samples: {existingCount}");
        
        // Detect or parse language
        SdkLanguage? detectedLanguage;
        if (!string.IsNullOrEmpty(language))
        {
            detectedLanguage = SdkLanguageHelpers.Parse(language);
            if (detectedLanguage == SdkLanguage.Unknown)
                detectedLanguage = null;
        }
        else
        {
            detectedLanguage = sdkInfo.Language;
        }
        
        if (detectedLanguage is null)
        {
            ConsoleUx.Error("Could not detect language. Use --language to specify.");
            return 1;
        }
        
        // Load config if present
        var config = await _configHelper.LoadConfigAsync(sdkPath, cancellationToken);
        
        // Get language context
        var context = CreateLanguageContext(detectedLanguage.Value);
        
        // Count existing samples and estimate context size
        var (existingSamplesCount, estimatedContextSize) = await ConsoleUx.SpinnerAsync(
            "Scanning files...", 
            () => EstimateContextAsync(sdkInfo.SourceFolder, samplesFolder, context, config, cancellationToken),
            cancellationToken);
        
        ConsoleUx.Info($"Context: ~{estimatedContextSize / 1000}K chars");
        
        // Build system prompt
        var systemPrompt = BuildSystemPrompt(context);
        
        // Create streaming user prompt (file content is NOT materialized here)
        var userPromptStream = StreamUserPromptAsync(prompt, count ?? 5, existingSamplesCount > 0, sdkInfo.SourceFolder, samplesFolder, context, config, cancellationToken);
        
        // Determine output folder
        var outputDir = outputPath ?? sdkInfo.SuggestedSamplesFolder;
        if (!dryRun)
        {
            Directory.CreateDirectory(outputDir);
        }
        
        // Show which AI provider is being used
        var providerName = _aiService.IsUsingOpenAi ? "OpenAI" : "GitHub Copilot";
        
        Console.WriteLine();
        
        // Start streaming progress
        using var progress = ConsoleUx.StartStreaming($"Generating with {providerName}...");
        
        List<GeneratedSample> samples = [];
        
        // Stream parsed samples as they complete
        await foreach (var sample in _aiService.StreamItemsAsync<GeneratedSample>(
            systemPrompt, userPromptStream, model, null, cancellationToken))
        {
            if (!string.IsNullOrEmpty(sample.Name) && !string.IsNullOrEmpty(sample.Code))
            {
                samples.Add(sample);
                var filename = SanitizeFileName(sample.Name) + context.FileExtension;
                progress.Update(filename);
            }
        }
        
        // Complete the spinner
        if (samples.Count > 0)
        {
            progress.Complete($"Generated {samples.Count} sample(s)");
        }
        else
        {
            progress.Fail("No samples generated");
            return 1;
        }
        
        // Write samples with visual feedback
        Console.WriteLine();
        for (var i = 0; i < samples.Count; i++)
        {
            var sample = samples[i];
            var filename = SanitizeFileName(sample.Name) + context.FileExtension;
            var isLast = i == samples.Count - 1;
            
            if (dryRun)
            {
                ConsoleUx.TreeItem($"{ConsoleUx.Cyan(filename)}", isLast);
            }
            else
            {
                var filePath = Path.Combine(outputDir, filename);
                await File.WriteAllTextAsync(filePath, sample.Code, cancellationToken);
                ConsoleUx.TreeItem($"{ConsoleUx.Green("✓")} {filename}", isLast);
            }
            
            // Small delay so user sees incremental progress
            await Task.Delay(50, cancellationToken);
        }
        
        Console.WriteLine();
        if (dryRun)
        {
            ConsoleUx.Info($"[DRY RUN] Would write to: {outputDir}");
        }
        else
        {
            ConsoleUx.Success($"Wrote {samples.Count} sample(s) to {outputDir}");
        }
        
        return 0;
    }
    
    private static string SanitizeFileName(string name)
    {
        // Replace invalid file name characters (includes : on Windows)
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new System.Text.StringBuilder(name);
        foreach (var c in invalid)
        {
            sanitized.Replace(c, '_');
        }
        // Also replace : and other problematic characters for cross-platform compatibility
        sanitized.Replace(':', '_');
        sanitized.Replace('/', '_');
        sanitized.Replace('\\', '_');
        // Replace spaces with underscores for cleaner filenames
        sanitized.Replace(' ', '_');
        return sanitized.ToString();
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
    
    /// <summary>
    /// Gets the prefix part of the user prompt (before the file context).
    /// </summary>
    private static string GetUserPromptPrefix(string? customPrompt, int count, bool hasExistingSamples)
    {
        if (!string.IsNullOrEmpty(customPrompt))
        {
            var dupeWarning = hasExistingSamples ? " Avoid duplicating <existing-samples>." : "";
            return $"{customPrompt}{dupeWarning} Generate {count} samples.\n\n";
        }
        
        if (!hasExistingSamples)
        {
            return $"Generate {count} samples covering: init/auth, CRUD, async, error handling, advanced features.\n\n";
        }
        else
        {
            return $"Generate {count} NEW samples for uncovered APIs. Avoid duplicating <existing-samples>.\n\n";
        }
    }
    
    /// <summary>
    /// Gets the suffix part of the user prompt (after the file context).
    /// </summary>
    private static string GetUserPromptSuffix() => "";
    
    /// <summary>
    /// Streams the complete user prompt including prefix, context, and suffix.
    /// </summary>
    private async IAsyncEnumerable<string> StreamUserPromptAsync(
        string? customPrompt,
        int count,
        bool hasExistingSamples,
        string sourceFolder,
        string samplesFolder,
        SampleLanguageContext languageContext,
        SdkCliConfig? config,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Yield the prompt prefix
        yield return GetUserPromptPrefix(customPrompt, count, hasExistingSamples);
        
        // Stream context (source code and samples)
        await foreach (var chunk in StreamContextAsync(sourceFolder, samplesFolder, languageContext, config, cancellationToken))
        {
            yield return chunk;
        }
        
        // Yield the suffix (currently empty but kept for flexibility)
        var suffix = GetUserPromptSuffix();
        if (!string.IsNullOrEmpty(suffix))
        {
            yield return suffix;
        }
    }
    
    /// <summary>
    /// Estimates context size without loading file content.
    /// Returns (sampleCount, estimatedCharacters).
    /// </summary>
    private async Task<(int SampleFilesCount, int EstimatedContextSize)> EstimateContextAsync(
        string sourceFolder,
        string samplesFolder, 
        SampleLanguageContext languageContext,
        SdkCliConfig? config,
        CancellationToken cancellationToken)
    {
        await Task.Yield(); // Allow spinner to start
        
        var sampleFilesCount = 0;
        if (Directory.Exists(samplesFolder))
        {
            sampleFilesCount = Directory.EnumerateFiles(samplesFolder, $"*{languageContext.FileExtension}", SearchOption.AllDirectories).Count();
        }
        
        // Estimate based on budgets - actual loading will be streamed
        var estimatedSize = SampleConstants.SourceCodeBudget;
        if (sampleFilesCount > 0)
        {
            estimatedSize += SampleConstants.ExistingSamplesBudget;
        }
        
        return (sampleFilesCount, estimatedSize);
    }
    
    /// <summary>
    /// Streams context from source code and samples without materializing in memory.
    /// Uses language-specific API extraction for supported languages.
    /// </summary>
    private async IAsyncEnumerable<string> StreamContextAsync(
        string sourceFolder,
        string samplesFolder, 
        SampleLanguageContext languageContext,
        SdkCliConfig? config,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Stream source code context using language-specific method (may use API extraction)
        await foreach (var chunk in languageContext.StreamContextAsync(
            [sourceFolder], config, SampleConstants.SourceCodeBudget, SampleConstants.MaxCharactersPerFile, cancellationToken))
        {
            yield return chunk;
        }
        
        // Stream existing samples if present
        if (Directory.Exists(samplesFolder))
        {
            var sampleFilesCount = Directory.EnumerateFiles(samplesFolder, $"*{languageContext.FileExtension}", SearchOption.AllDirectories).Count();
            if (sampleFilesCount > 0)
            {
                var basePath = Path.GetDirectoryName(sourceFolder) ?? sourceFolder;
                
                var groups = new List<SourceInputGroup>
                {
                    new(
                        SectionName: "existing-samples",
                        Inputs: [new SourceInputSpec(samplesFolder, [languageContext.FileExtension], SampleConstants.ExistingSamplesExcludePatterns)],
                        Budget: SampleConstants.ExistingSamplesBudget,
                        PerFileLimit: SampleConstants.MaxCharactersPerFile
                    )
                };
                
                await foreach (var chunk in _fileHelper.StreamFilesAsync(groups, basePath, cancellationToken))
                {
                    yield return chunk.Content;
                }
            }
        }
    }
    
    private static int GetSourcePriority(FileMetadata file, SdkLanguage language)
    {
        var path = file.RelativePath.Replace('\\', '/').ToLowerInvariant();
        var name = Path.GetFileNameWithoutExtension(file.FilePath).ToLowerInvariant();
        
        // Deprioritize generated code - load human-written code first
        var isGenerated = language switch
        {
            SdkLanguage.DotNet => path.Contains("/generated/") || name.EndsWith(".g") || name.Contains("generated"),
            SdkLanguage.Python => path.Contains("/_generated/") || path.Contains("/generated/") || name.StartsWith("_"),
            SdkLanguage.Java => path.Contains("/generated/") || path.Contains("/implementation/") || name.Contains("generated"),
            SdkLanguage.TypeScript or SdkLanguage.JavaScript => path.Contains("/generated/") || name.Contains(".generated."),
            SdkLanguage.Go => path.Contains("/generated/") || name.StartsWith("zz_") || name.Contains("_autorest"),
            _ => false
        };
        var basePriority = isGenerated ? 100 : 0;
        
        // Prioritize key files
        if (name.Contains("client")) return basePriority + 1;
        if (name.Contains("options") || name.Contains("builder")) return basePriority + 2;
        if (name.Contains("model")) return basePriority + 3;
        return basePriority + 10;
    }
    
    private static string[] ExtractExtensions(string[] patterns)
    {
        return patterns
            .Where(p => p.Contains("*."))
            .Select(p => "." + p.Split("*.").Last().TrimEnd('*', '/'))
            .Distinct()
            .ToArray();
    }
    
    private static string[] GetDefaultExtensions(SdkLanguage language) => language switch
    {
        SdkLanguage.DotNet => new[] { ".cs" },
        SdkLanguage.Python => new[] { ".py" },
        SdkLanguage.JavaScript => new[] { ".js", ".mjs" },
        SdkLanguage.TypeScript => new[] { ".ts", ".tsx" },
        SdkLanguage.Java => new[] { ".java" },
        SdkLanguage.Go => new[] { ".go" },
        _ => Array.Empty<string>()
    };
    
    private static string[] GetDefaultExcludes(SdkLanguage language) => language switch
    {
        SdkLanguage.DotNet => new[] { "**/obj/**", "**/bin/**", "**/*.g.cs", "**/AssemblyInfo.cs" },
        SdkLanguage.Python => new[] { "**/__pycache__/**", "**/.venv/**", "**/venv/**", "**/*.pyc" },
        SdkLanguage.JavaScript or SdkLanguage.TypeScript => new[] { "**/node_modules/**", "**/dist/**", "**/*.d.ts" },
        SdkLanguage.Java => new[] { "**/target/**", "**/build/**" },
        SdkLanguage.Go => new[] { "**/vendor/**", "**/*_test.go" },
        _ => Array.Empty<string>()
    };
}
