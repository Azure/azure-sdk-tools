// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.ComponentModel;
using System.Text;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Services.Languages.Samples;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.Package.Samples
{
    /// <summary>
    /// Represents a translated sample with both original and translated filenames.
    /// </summary>
    /// <param name="OriginalFileName">The original source sample filename</param>
    /// <param name="TranslatedFileName">The translated sample filename in target language</param>
    /// <param name="Content">The translated source code content</param>
    public record TranslatedSample(string OriginalFileName, string TranslatedFileName, string Content);

    /// <summary>
    /// Represents a source sample file discovered for translation.
    /// </summary>
    /// <param name="FilePath">The full path to the sample file</param>
    /// <param name="Content">The content of the sample file</param>
    /// <param name="SourceLanguage">The detected source language</param>
    /// <param name="Extension">The file extension</param>
    internal record SourceSampleFile(string FilePath, string Content, string SourceLanguage, string Extension);

    /// <summary>
    /// Tool for translating sample code between Azure SDK packages in different languages.
    /// Provides both CLI command (azsdk pkg samples translate) and MCP tool method.
    /// </summary>
    [McpServerToolType, Description("Translates sample code files from a source Azure SDK package to a target package in a different programming language. Takes samples from the source package's samples directory, understands the functionality being demonstrated, and generates equivalent idiomatic code for the target language.")]
    public class SampleTranslatorTool : LanguageMcpTool
    {
        private readonly IMicroagentHostService _microagentHostService;

        public SampleTranslatorTool(
            IMicroagentHostService microagentHostService,
            ILogger<SampleTranslatorTool> logger,
            IGitHelper gitHelper,
            IEnumerable<LanguageService> languageServices
        ) : base(languageServices, gitHelper, logger)
        {
            _microagentHostService = microagentHostService;
        }

        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package, SharedCommandGroups.PackageSample];

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
                var response = await TranslateSamplesAsync(
                    fromPackagePath,
                    toPackagePath,
                    overwrite,
                    model,
                    batchSize,
                    ct);

                return CreateCommandResponse(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SampleTranslator threw an exception");
                return new DefaultCommandResponse { ResponseError = $"SampleTranslator threw an exception: {ex.Message}" };
            }
        }

        /// <summary>
        /// Translates sample code from one package language to another.
        /// </summary>
        [McpServerTool(Name = "azsdk_package_translate_samples"), Description("Translates sample code files from a source package to a target package in a different programming language. Takes samples from the source package's samples directory, understands the functionality being demonstrated, and generates equivalent idiomatic code for the target language using the target package's APIs. Preserves the sample's intent and structure while adapting authentication patterns, error handling, and async conventions to match the target language's best practices.")]
        public async Task<PackageOperationResponse> TranslateSamplesAsync(
            [Description("Path to the source package directory containing samples to translate.")] string fromPackagePath,
            [Description("Path to the target package directory where translated samples will be written.")] string toPackagePath,
            [Description("Whether to overwrite existing sample files.")] bool overwrite = false,
            [Description("Optional Azure OpenAI deployment name.")] string? model = null,
            [Description("Number of samples to process per batch.")] int batchSize = 5,
            CancellationToken ct = default)
        {
            try
            {
                logger.LogInformation("Translating samples from {fromPath} to {toPath}", fromPackagePath, toPackagePath);

                if (string.IsNullOrWhiteSpace(fromPackagePath))
                {
                    return PackageOperationResponse.CreateFailure("SampleTranslator failed with validation errors: Source package path (--from) is required and cannot be empty.");
                }

                if (string.IsNullOrWhiteSpace(toPackagePath))
                {
                    return PackageOperationResponse.CreateFailure("SampleTranslator failed with validation errors: Target package path (--to) is required and cannot be empty.");
                }

                string fullFromPath = Path.GetFullPath(fromPackagePath);
                if (!Directory.Exists(fullFromPath))
                {
                    return PackageOperationResponse.CreateFailure($"SampleTranslator failed with validation errors: Source package path does not exist: {fullFromPath}. Unable to determine source language for package (resolver returned null). Ensure repository structure and Language-Settings.ps1 are correct.");
                }

                string fullToPath = Path.GetFullPath(toPackagePath);
                if (!Directory.Exists(fullToPath))
                {
                    return PackageOperationResponse.CreateFailure($"SampleTranslator failed with validation errors: Target package path does not exist: {fullToPath}. Unable to determine target language for package (resolver returned null). Ensure repository structure and Language-Settings.ps1 are correct.");
                }

                fromPackagePath = fullFromPath;
                toPackagePath = fullToPath;

                var result = await TranslateSamplesInternalAsync(
                    fromPackagePath, toPackagePath, overwrite, model, batchSize, ct);

                if (result.TranslatedCount == 0)
                {
                    return PackageOperationResponse.CreateSuccess(
                        $"No samples found to translate from {result.SourceLanguage}.",
                        nextSteps: ["Verify the source package has sample files", "Check the samples directory exists"]);
                }

                return PackageOperationResponse.CreateSuccess(
                    $"Successfully translated {result.TranslatedCount} sample(s) from {result.SourceLanguage} to {result.TargetLanguage}: {result.FileNames}",
                    nextSteps: ["Review the translated samples", "Test the samples to ensure they compile and run correctly"]);
            }
            catch (ArgumentException ex)
            {
                logger.LogError(ex, "Validation error while translating samples from {FromPath} to {ToPath}", fromPackagePath, toPackagePath);
                return PackageOperationResponse.CreateFailure($"SampleTranslator failed with validation errors: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError(ex, "Validation error while translating samples from {FromPath} to {ToPath}", fromPackagePath, toPackagePath);
                return PackageOperationResponse.CreateFailure($"SampleTranslator failed with validation errors: {ex.Message}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while translating samples from {FromPath} to {ToPath}", fromPackagePath, toPackagePath);
                return PackageOperationResponse.CreateFailure($"SampleTranslator threw an exception: {ex.Message}");
            }
        }

        private async Task<(int TranslatedCount, string OutputDirectory, string SourceLanguage, string TargetLanguage, string FileNames)> TranslateSamplesInternalAsync(
            string fromPackagePath,
            string toPackagePath,
            bool overwrite,
            string? model,
            int batchSize,
            CancellationToken ct)
        {
            // Determine source language and get samples directory from source package path
            var sourceLanguageService = await GetLanguageServiceAsync(fromPackagePath, ct)
                ?? throw new ArgumentException("Unable to determine source language for package (resolver returned null). " +
                                               "Ensure repository structure and Language-Settings.ps1 are correct.");
            var sourcePackageInfo = await sourceLanguageService.GetPackageInfo(fromPackagePath, ct);
            var samplesPath = sourcePackageInfo.SamplesDirectory;

            // Determine target language from target package path
            var targetLanguageService = await GetLanguageServiceAsync(toPackagePath, ct)
                ?? throw new ArgumentException("Unable to determine target language for package (resolver returned null). " +
                                               "Ensure repository structure and Language-Settings.ps1 are correct.");
            var packageInfo = await targetLanguageService.GetPackageInfo(toPackagePath, ct);
            var outputDirectory = packageInfo.SamplesDirectory;

            SampleLanguageContext sourceContext = sourceLanguageService.SampleLanguageContext;
            SampleLanguageContext targetContext = targetLanguageService.SampleLanguageContext;

            var sourceLanguage = sourceContext.Language;
            var targetLanguage = targetContext.Language;
            logger.LogInformation(
                "Source language: {sourceLanguage}, Source samples: {samplesPath}, " +
                "Target language: {targetLanguage}, Package: {packageName}, Output: {outputDirectory}",
                sourceLanguage, samplesPath, targetLanguage, packageInfo.PackageName, outputDirectory);

            // Discover source sample files
            var sampleFiles = await DiscoverSampleFilesAsync(samplesPath, sourceContext, ct);
            if (!sampleFiles.Any())
            {
                logger.LogWarning("No sample files found at {samplesPath}", samplesPath);
                return (0, outputDirectory, sourceLanguage, targetLanguage, "");
            }

            logger.LogInformation("Found {count} sample files to translate", sampleFiles.Count);

            // Load target package context for translation
            var targetPackageContext = await targetContext.LoadContextAsync(
                [packageInfo.PackagePath],
                SampleConstants.MaxContextCharacters,
                SampleConstants.MaxCharactersPerFile,
                ct);

            if (string.IsNullOrWhiteSpace(targetPackageContext))
            {
                throw new InvalidOperationException(
                    $"No source code content could be loaded from the target package path '{packageInfo.PackagePath}'. " +
                    $"Please verify the path contains valid source files for language '{targetLanguage}'.");
            }

            var targetLanguageInstructions = targetContext.GetSampleGenerationInstructions();

            // Process samples in batches
            var allTranslatedSamples = new List<TranslatedSample>();
            var batches = sampleFiles.Chunk(batchSize);

            foreach (var batch in batches)
            {
                var batchResults = await TranslateSampleBatchAsync(
                    batch,
                    sourceLanguage,
                    targetLanguage,
                    targetLanguageInstructions,
                    targetPackageContext,
                    targetContext,
                    packageInfo,
                    samplesPath,
                    outputDirectory,
                    overwrite,
                    model,
                    ct);
                allTranslatedSamples.AddRange(batchResults);
            }

            logger.LogInformation("Sample translation completed");
            var fileNames = string.Join(", ", allTranslatedSamples.Select(s => $"{s.OriginalFileName} -> {s.TranslatedFileName}"));
            return (allTranslatedSamples.Count, outputDirectory, sourceLanguage, targetLanguage, fileNames);
        }

        private async Task<List<SourceSampleFile>> DiscoverSampleFilesAsync(
            string samplesPath,
            SampleLanguageContext sourceContext,
            CancellationToken ct)
        {
            var sampleFiles = new List<SourceSampleFile>();
            var fullSamplesPath = Path.GetFullPath(samplesPath);

            var sourceExtension = sourceContext.FileExtension;
            if (string.IsNullOrEmpty(sourceExtension))
            {
                throw new ArgumentException(
                    $"Source language context does not specify a file extension for language: {sourceContext.Language}");
            }

            logger.LogDebug("Scanning for sample files with source language extension: {extension}", sourceExtension);

            if (File.Exists(fullSamplesPath))
            {
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

        private async Task<List<TranslatedSample>> TranslateSampleBatchAsync(
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
            var originalsByName = batchList.ToDictionary(
                sample => Path.GetFileName(sample.FilePath),
                sample => sample,
                StringComparer.Ordinal);

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

            var microagent = string.IsNullOrEmpty(model)
                ? new Microagent<List<TranslatedSample>>() { Instructions = enhancedPrompt }
                : new Microagent<List<TranslatedSample>>() { Instructions = enhancedPrompt, Model = model };

            logger.LogInformation("Calling translation microagent service...");
            var translatedSamples = await _microagentHostService.RunAgentToCompletion(microagent, ct);
            logger.LogInformation("Translation microagent service returned");

            var writtenSamples = new List<TranslatedSample>();

            if (translatedSamples == null || translatedSamples.Count == 0)
            {
                logger.LogWarning("Translation microagent returned no samples for this batch");
                return writtenSamples;
            }

            logger.LogInformation("Generated {sampleCount} translated samples", translatedSamples.Count);
            Directory.CreateDirectory(outputDirectory);

            foreach (var translatedSample in translatedSamples)
            {
                if (translatedSample == null ||
                    string.IsNullOrEmpty(translatedSample.TranslatedFileName) ||
                    string.IsNullOrEmpty(translatedSample.Content))
                {
                    logger.LogWarning("Skipping invalid translated sample: {original}",
                        translatedSample?.OriginalFileName ?? "unknown");
                    continue;
                }

                // Find the original source file to get its directory structure
                var lookupKey = Path.GetFileName(translatedSample.OriginalFileName);
                if (!originalsByName.TryGetValue(lookupKey, out var originalSample))
                {
                    logger.LogWarning("Could not find original sample file for: {original}",
                        translatedSample.OriginalFileName);
                    continue;
                }

                // Calculate the relative path from the source samples directory
                var fullSourceSamplesPath = Path.GetFullPath(sourceSamplesPath);
                var relativePath = Path.GetRelativePath(fullSourceSamplesPath, originalSample.FilePath);
                var relativeDir = Path.GetDirectoryName(relativePath) ?? "";

                // Create the output file path preserving directory structure
                var fileExtension = targetContext.FileExtension;
                var cleanFileName = Path.GetFileNameWithoutExtension(
                    translatedSample.TranslatedFileName.Replace('/', '_').Replace('\\', '_'));
                var sampleFileName = $"{cleanFileName}{fileExtension}";

                // Combine output directory with relative directory structure
                var outputSubDirectory = string.IsNullOrEmpty(relativeDir)
                    ? outputDirectory
                    : Path.Combine(outputDirectory, relativeDir);
                var sampleFilePath = Path.Combine(outputSubDirectory, sampleFileName);

                // Ensure the subdirectory exists
                Directory.CreateDirectory(outputSubDirectory);

                logger.LogDebug("Writing translated sample: {original} -> {translated} (preserving structure: {relativeDir})",
                    translatedSample.OriginalFileName, sampleFileName, relativeDir);

                if (File.Exists(sampleFilePath) && !overwrite)
                {
                    logger.LogWarning("Translated sample file already exists: {filePath}. Use --overwrite to replace it.",
                        sampleFilePath);
                }
                else
                {
                    await File.WriteAllTextAsync(sampleFilePath, translatedSample.Content, ct);
                    logger.LogInformation("Translated sample written to: {filePath} (from {original})",
                        sampleFilePath, translatedSample.OriginalFileName);
                    writtenSamples.Add(translatedSample);
                }
            }

            return writtenSamples;
        }

        private DefaultCommandResponse CreateCommandResponse(PackageOperationResponse response)
        {
            string? errorSummary = null;
            if (response.OperationStatus == Status.Failed)
            {
                errorSummary = response.ResponseError;
                if (string.IsNullOrWhiteSpace(errorSummary) && response.ResponseErrors?.Count > 0)
                {
                    errorSummary = string.Join("; ", response.ResponseErrors);
                }

                if (!string.IsNullOrWhiteSpace(errorSummary))
                {
                    logger.LogError("SampleTranslator failed: {error}", errorSummary);
                }
            }

            var primaryError = response.ResponseError;
            if (string.IsNullOrWhiteSpace(primaryError))
            {
                primaryError = response.ResponseErrors?.FirstOrDefault();
            }

            var commandResponse = new DefaultCommandResponse
            {
                Message = response.Message,
                Result = response.Result,
                Duration = response.Duration,
                ResponseError = primaryError
            };

            if (response.ResponseErrors != null)
            {
                var remainingErrors = response.ResponseErrors
                    .Where(e => !string.Equals(e, primaryError, StringComparison.Ordinal))
                    .ToList();

                if (remainingErrors.Count > 0)
                {
                    commandResponse.ResponseErrors = remainingErrors;
                }
            }

            if (response.NextSteps != null)
            {
                commandResponse.NextSteps = new List<string>(response.NextSteps);
            }

            return commandResponse;
        }
    }
}
