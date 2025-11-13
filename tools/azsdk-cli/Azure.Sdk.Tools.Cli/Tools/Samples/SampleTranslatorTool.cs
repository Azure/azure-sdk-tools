// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.ComponentModel;
using System.Text;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Models;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Samples;
using Azure.Sdk.Tools.Cli.Tools.Core;
using Azure.Sdk.Tools.Cli.Services.Languages;

namespace Azure.Sdk.Tools.Cli.Tools.Samples
{
    /// <summary>
    /// Represents a translated sample with its filename and content.
    /// </summary>
    /// <param name="OriginalFileName">The original filename from the source sample</param>
    /// <param name="TranslatedFileName">The suggested filename for the translated sample</param>
    /// <param name="Content">The complete source code content of the translated sample</param>
    public record TranslatedSample(string OriginalFileName, string TranslatedFileName, string Content);

    /// <summary>
    /// Represents a source sample file discovered for translation.
    /// </summary>
    /// <param name="FilePath">The full path to the sample file</param>
    /// <param name="Content">The content of the sample file</param>
    /// <param name="SourceLanguage">The detected source language</param>
    /// <param name="Extension">The file extension</param>
    internal record SourceSampleFile(string FilePath, string Content, string SourceLanguage, string Extension);

    [McpServerToolType, Description("Translates sample files from one language to another")]
    public class SampleTranslatorTool : LanguageMcpTool
    {
        private readonly ILanguageSpecificResolver<SampleLanguageContext> sampleContextResolver;
        private readonly IMicroagentHostService microagentHostService;
        public SampleTranslatorTool(
            IMicroagentHostService microagentHostService,
            ILogger<SampleTranslatorTool> logger,
            IGitHelper gitHelper,
            IEnumerable<LanguageService> languageServices,
            ILanguageSpecificResolver<SampleLanguageContext> sampleContextResolver,
            IFileHelper fileHelper
        ) : base(languageServices, gitHelper, logger)
        {
            this.sampleContextResolver = sampleContextResolver;
            this.microagentHostService = microagentHostService;
        }

        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Samples];

        private readonly Option<string> fromOption = new("--from")
        {
            Description = "Path to the source package directory containing samples to translate",
            Required = true,
        };

        private readonly Option<string> toOption = new("--to")
        {
            Description = "Path to the target package directory where translated samples will be written",
            Required = true,
        };

        private readonly Option<bool> overwriteOption = new("--overwrite")
        {
            Description = "Overwrite existing files without prompting",
            Required = false,
            DefaultValueFactory = _ => false,
        };

        private readonly Option<string> modelOption = new("--model")
        {
            Description = "Azure OpenAI deployment name to use for translation",
            Required = false
        };

        private readonly Option<int> batchSizeOption = new("--batch-size")
        {
            Description = "Number of samples to process in each batch to avoid overwhelming the AI model",
            Required = false,
            DefaultValueFactory = _ => SampleConstants.DefaultBatchSize,
        };

        protected override Command GetCommand() => new("translate", "Translates sample files from source language to target package language")
        {
            fromOption,
            toOption,
            overwriteOption,
            modelOption,
            batchSizeOption
        };

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            string fromPackagePath = parseResult.GetValue(fromOption) ?? throw new ArgumentException("from is required");
            string toPackagePath = parseResult.GetValue(toOption) ?? throw new ArgumentException("to is required");
            bool overwrite = parseResult.GetValue(overwriteOption);
            var model = parseResult.GetValue(modelOption);
            int batchSize = parseResult.GetValue(batchSizeOption);

            try
            {
                await TranslateSamplesAsync(fromPackagePath, toPackagePath, overwrite, model, batchSize, ct);
                return new DefaultCommandResponse { Message = "Sample translation completed successfully" };
            }
            catch (ArgumentException ex)
            {
                logger.LogError(ex, "SampleTranslator failed with validation errors");
                return new DefaultCommandResponse { ResponseError = $"SampleTranslator failed with validation errors: {ex.Message}" };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SampleTranslator threw an exception");
                return new DefaultCommandResponse { ResponseError = $"SampleTranslator threw an exception: {ex.Message}" };
            }
        }

        private async Task TranslateSamplesAsync(string fromPackagePath, string toPackagePath, bool overwrite, string? model, int batchSize, CancellationToken ct)
        {
            // Determine source language and get samples directory from source package path
            var sourceLanguageService = GetLanguageService(fromPackagePath) ?? throw new ArgumentException("Unable to determine source language for package (resolver returned null). Ensure repository structure and Language-Settings.ps1 are correct.");
            var sourcePackageInfo = await sourceLanguageService.GetPackageInfo(fromPackagePath, ct);
            var samplesPath = sourcePackageInfo.SamplesDirectory;

            // Determine target language from target package path
            var targetLanguageService = GetLanguageService(toPackagePath) ?? throw new ArgumentException("Unable to determine target language for package (resolver returned null). Ensure repository structure and Language-Settings.ps1 are correct.");
            var packageInfo = await targetLanguageService.GetPackageInfo(toPackagePath, ct);
            var outputDirectory = packageInfo.SamplesDirectory;

            logger.LogDebug("Loading source and target language contexts");
            SampleLanguageContext sourceContext = await sampleContextResolver.Resolve(fromPackagePath, ct) ?? throw new ArgumentException("Unable to determine source language for package (resolver returned null). Ensure repository structure and Language-Settings.ps1 are correct.");
            SampleLanguageContext targetContext = await sampleContextResolver.Resolve(toPackagePath, ct) ?? throw new ArgumentException("Unable to determine target language for package (resolver returned null). Ensure repository structure and Language-Settings.ps1 are correct.");

            var sourceLanguage = sourceContext.Language;
            var targetLanguage = targetContext.Language;
            logger.LogInformation("Source language: {sourceLanguage}, Source samples: {samplesPath}, Target language: {targetLanguage}, Package: {packageName}, Output: {outputDirectory}", sourceLanguage, samplesPath, targetLanguage, packageInfo.PackageName, outputDirectory);

            // Discover source sample files - only include files with source language extension
            var sampleFiles = await DiscoverSampleFilesAsync(samplesPath, sourceContext, ct);
            if (!sampleFiles.Any())
            {
                logger.LogWarning("No sample files found at {samplesPath}", samplesPath);
                return;
            }

            logger.LogInformation("Found {count} sample files to translate", sampleFiles.Count);

            // Load target package context for translation
            var targetPackageContext = await targetContext.LoadContextAsync([packageInfo.PackagePath], SampleConstants.MaxContextCharacters, SampleConstants.MaxCharactersPerFile, ct);
            
            if (string.IsNullOrWhiteSpace(targetPackageContext))
            {
                throw new InvalidOperationException($"No source code content could be loaded from the target package path '{packageInfo.PackagePath}'. Please verify the path contains valid source files for language '{targetLanguage}'.");
            }

            var targetLanguageInstructions = targetContext.GetSampleGenerationInstructions();

            // Process samples in batches to avoid overwhelming the AI model
            var batches = sampleFiles.Chunk(batchSize);

            foreach (var batch in batches)
            {
                await TranslateSampleBatchAsync(batch, sourceLanguage, targetLanguage, targetLanguageInstructions, targetPackageContext, targetContext, packageInfo, samplesPath, outputDirectory, overwrite, model, ct);
            }

            logger.LogInformation("Sample translation completed");
        }

        private async Task<List<SourceSampleFile>> DiscoverSampleFilesAsync(string samplesPath, SampleLanguageContext sourceContext, CancellationToken ct)
        {
            var sampleFiles = new List<SourceSampleFile>();
            var fullSamplesPath = Path.GetFullPath(samplesPath);

            // Only include files with the source language file extension
            var sourceExtension = sourceContext.FileExtension;
            if (string.IsNullOrEmpty(sourceExtension))
            {
                throw new ArgumentException($"Source language context does not specify a file extension for language: {sourceContext.Language}");
            }

            logger.LogDebug("Scanning for sample files with source language extension: {extension}", sourceExtension);

            if (File.Exists(fullSamplesPath))
            {
                // Single file
                var extension = Path.GetExtension(fullSamplesPath);
                if (string.Equals(extension, sourceExtension, StringComparison.OrdinalIgnoreCase))
                {
                    var content = await File.ReadAllTextAsync(fullSamplesPath, ct);
                    sampleFiles.Add(new SourceSampleFile(fullSamplesPath, content, sourceContext.Language, extension));
                    logger.LogDebug("Added single sample file: {file} ({language})", fullSamplesPath, sourceContext.Language);
                }
            }
            else if (Directory.Exists(fullSamplesPath))
            {
                // Directory - scan recursively for files with source extension
                var pattern = $"*{sourceExtension}";
                var files = Directory.GetFiles(fullSamplesPath, pattern, SearchOption.AllDirectories);
                
                foreach (var filePath in files)
                {
                    try
                    {
                        var content = await File.ReadAllTextAsync(filePath, ct);
                        sampleFiles.Add(new SourceSampleFile(filePath, content, sourceContext.Language, sourceExtension));
                        logger.LogDebug("Added sample file: {file}", filePath);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to read sample file: {file}", filePath);
                    }
                }
            }
            else
            {
                throw new ArgumentException($"Samples path does not exist: {fullSamplesPath}");
            }

            logger.LogInformation("Discovered {count} sample files for translation", sampleFiles.Count);
            return sampleFiles;
        }

        private async Task TranslateSampleBatchAsync(
            IEnumerable<SourceSampleFile> batch, 
            string sourceLanguage,
            string targetLanguage, 
            string targetLanguageInstructions,
            string targetPackageContext,
            SampleLanguageContext targetContext,
            PackageInfo packageInfo,
            string sourceSamplesPath,
            string outputDirectory, 
            bool overwrite, 
            string? model, 
            CancellationToken ct)
        {
            var batchList = batch.ToList();
            logger.LogInformation("Translating batch of {count} samples to {targetLanguage}", batchList.Count, targetLanguage);

            // Build samples context for the batch
            var samplesContext = new StringBuilder();
            foreach (var sample in batchList)
            {
                samplesContext.AppendLine($"## Source Sample: {Path.GetFileName(sample.FilePath)} ({sample.SourceLanguage})");
                samplesContext.AppendLine("```");
                samplesContext.AppendLine(sample.Content);
                samplesContext.AppendLine("```");
                samplesContext.AppendLine();
            }

            var enhancedPrompt = $@"
You are translating sample code files from {sourceLanguage} to {targetLanguage} for the {packageInfo.PackageName} client library.

TRANSLATION REQUIREMENTS:
- Translate functionality, not just syntax - understand what each sample does and implement equivalent functionality
- Use the target language's idioms, conventions, and best practices
- Adapt authentication, error handling, and async patterns to the target language
- Maintain the same logical structure and comments where appropriate
- Use appropriate imports/includes for the target language and library
- Ensure the translated code follows the target language's sample template structure

TARGET LANGUAGE GUIDELINES:
{targetLanguageInstructions}

TARGET PACKAGE CONTEXT (for understanding available APIs and patterns):
<package-context>
{targetPackageContext}
</package-context>

SOURCE SAMPLES TO TRANSLATE:
{samplesContext}

For each source sample, provide a translation with:
1. Original filename
2. Appropriate new filename for {targetLanguage} (following naming conventions)
3. Translated code content

Return a JSON array of objects with 'OriginalFileName', 'TranslatedFileName', and 'Content' properties.
";

            logger.LogDebug("Enhanced prompt prepared with {contextLength} characters of context", targetPackageContext.Length);
            logger.LogDebug("Starting translation microagent with model: {model}", model);

            var microagent = string.IsNullOrEmpty(model)
                ? new Microagent<List<TranslatedSample>>() { Instructions = enhancedPrompt }
                : new Microagent<List<TranslatedSample>>() { Instructions = enhancedPrompt, Model = model };

            try
            {
                logger.LogInformation("Calling translation microagent service...");
                var translatedSamples = await microagentHostService.RunAgentToCompletion(microagent, ct);
                logger.LogInformation("Translation microagent service returned");

                if (translatedSamples == null || translatedSamples.Count == 0)
                {
                    logger.LogWarning("Translation microagent returned no samples for this batch");
                    return;
                }

                logger.LogInformation("Generated {sampleCount} translated samples", translatedSamples.Count);
                Directory.CreateDirectory(outputDirectory);

                foreach (var translatedSample in translatedSamples)
                {
                    if (translatedSample == null || string.IsNullOrEmpty(translatedSample.TranslatedFileName) || string.IsNullOrEmpty(translatedSample.Content))
                    {
                        logger.LogWarning("Skipping invalid translated sample: {original}", translatedSample?.OriginalFileName ?? "unknown");
                        continue;
                    }

                    // Find the original source file to get its directory structure
                    var originalSample = batchList.FirstOrDefault(s => Path.GetFileName(s.FilePath) == translatedSample.OriginalFileName);
                    if (originalSample == null)
                    {
                        logger.LogWarning("Could not find original sample file for: {original}", translatedSample.OriginalFileName);
                        continue;
                    }

                    // Calculate the relative path from the source samples directory
                    var fullSourceSamplesPath = Path.GetFullPath(sourceSamplesPath);
                    var relativePath = Path.GetRelativePath(fullSourceSamplesPath, originalSample.FilePath);
                    var relativeDir = Path.GetDirectoryName(relativePath) ?? "";

                    // Create the output file path preserving directory structure
                    var fileExtension = targetContext.FileExtension;
                    var cleanFileName = Path.GetFileNameWithoutExtension(translatedSample.TranslatedFileName.Replace('/', '_').Replace('\\', '_'));
                    var sampleFileName = $"{cleanFileName}{fileExtension}";
                    
                    // Combine output directory with relative directory structure
                    var outputSubDirectory = string.IsNullOrEmpty(relativeDir) ? outputDirectory : Path.Combine(outputDirectory, relativeDir);
                    var sampleFilePath = Path.Combine(outputSubDirectory, sampleFileName);

                    // Ensure the subdirectory exists
                    Directory.CreateDirectory(outputSubDirectory);

                    logger.LogDebug("Writing translated sample: {original} -> {translated} (preserving structure: {relativeDir})", 
                        translatedSample.OriginalFileName, sampleFileName, relativeDir);

                    if (File.Exists(sampleFilePath) && !overwrite)
                    {
                        logger.LogWarning("Translated sample file already exists: {filePath}. Use --overwrite to replace it.", sampleFilePath);
                    }
                    else
                    {
                        await File.WriteAllTextAsync(sampleFilePath, translatedSample.Content);
                        logger.LogInformation("Translated sample written to: {filePath} (from {original})", sampleFilePath, translatedSample.OriginalFileName);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error running translation microagent for batch");
                throw;
            }
        }
    }
}
