// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.ComponentModel;
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
    /// Represents a generated sample with its filename and content.
    /// </summary>
    /// <param name="FileName">The suggested filename for the sample</param>
    /// <param name="Content">The complete source code content of the sample</param>
    public record GeneratedSample(string FileName, string Content);

    /// <summary>
    /// Tool for generating sample code for Azure SDK packages.
    /// Provides both CLI command (azsdk pkg samples generate) and MCP tool method.
    /// </summary>
    [McpServerToolType, Description("Generates sample files for Azure SDK packages based on prompts.")]
    public class SampleGeneratorTool : LanguageMcpTool
    {
        private readonly IMicroagentHostService _microagentHostService;

        public SampleGeneratorTool(
            IMicroagentHostService microagentHostService,
            ILogger<SampleGeneratorTool> logger,
            IGitHelper gitHelper,
            IEnumerable<LanguageService> languageServices
        ) : base(languageServices, gitHelper, logger)
        {
            _microagentHostService = microagentHostService;
        }
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package, SharedCommandGroups.PackageSample];

        private readonly Option<string> promptOption = new("--prompt")
        {
            Description = "Prompt to use for the sample generation, e.g. \"upload a blob\". It is either a path to a .md file containing the sample description, or a text prompt",
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
            Description = "Azure OpenAI deployment name to use ",
            Required = false
        };

        private readonly Option<string[]> extraContextOption = new("--extra-context")
        {
            Description = "Path to a file or folder containing additional context to include in the prompt. Can be specified multiple times.",
            Required = false,
            AllowMultipleArgumentsPerToken = true
        };

        protected override Command GetCommand() => new("generate", "Generates sample files")
        {
            SharedOptions.PackagePath,
            promptOption,
            overwriteOption,
            modelOption,
            extraContextOption
        };

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            string rawPrompt = parseResult.GetValue(promptOption) ?? string.Empty;
            string packagePath = parseResult.GetValue(SharedOptions.PackagePath) ?? ".";
            bool overwrite = parseResult.GetValue(overwriteOption);
            var model = parseResult.GetValue(modelOption);
            var extraContextPaths = parseResult.GetValue(extraContextOption);

            try
            {
                var response = await GenerateSamplesAsync(
                    packagePath,
                    rawPrompt,
                    overwrite,
                    model,
                    extraContextPaths,
                    ct);

                return CreateCommandResponse(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SampleGenerator threw an exception");
                return new DefaultCommandResponse { ResponseError = $"SampleGenerator threw an exception: {ex.Message}" };
            }
        }

        /// <summary>
        /// Generates sample code for a specified package based on a prompt.
        /// </summary>
        [McpServerTool(Name = "azsdk_package_generate_samples"), Description("Generates sample code for a specified package based on a prompt describing sample scenarios.")]
        public async Task<PackageOperationResponse> GenerateSamplesAsync(
            [Description("The absolute path to the package directory.")] string packagePath,
            [Description("The prompt describing sample scenarios to generate (text or path to .md file).")] string prompt,
            [Description("Whether to overwrite existing sample files.")] bool overwrite = false,
            [Description("Optional Azure OpenAI deployment name.")] string? model = null,
            [Description("Optional additional context file/folder paths.")] string[]? extraContextPaths = null,
            CancellationToken ct = default)
        {
            try
            {
                logger.LogInformation("Generating samples for package at: {packagePath}", packagePath);

                if (string.IsNullOrWhiteSpace(packagePath))
                {
                    return PackageOperationResponse.CreateFailure("Package path is required and cannot be empty.");
                }

                if (string.IsNullOrWhiteSpace(prompt))
                {
                    return PackageOperationResponse.CreateFailure("Prompt is required and cannot be empty.");
                }

                string fullPath = Path.GetFullPath(packagePath);
                if (!Directory.Exists(fullPath))
                {
                    return PackageOperationResponse.CreateFailure($"Package path does not exist: {fullPath}");
                }

                packagePath = fullPath;
                var resolvedPrompt = await ResolvePromptAsync(prompt, ct);

                var result = await GenerateSamplesInternalAsync(
                    resolvedPrompt, packagePath, overwrite, model, extraContextPaths, ct);

                var response = result.SamplesCount == 0
                    ? PackageOperationResponse.CreateSuccess(
                        "Sample generation completed but no samples were generated.",
                        packageInfo: result.PackageInfo,
                        nextSteps: ["Check the prompt for clarity", "Ensure the package has valid source files"])
                    : PackageOperationResponse.CreateSuccess(
                        $"Successfully generated {result.SamplesCount} sample(s) for {result.Language} in {result.OutputDirectory}: {result.FileNames}",
                        packageInfo: result.PackageInfo,
                        nextSteps: ["Review the generated samples", "Test the samples to ensure they compile and run correctly"]);

                response.Result = new { samples_count = result.SamplesCount };
                return response;
            }
            catch (ArgumentException ex)
            {
                logger.LogError(ex, "Validation error while generating samples for package: {PackagePath}", packagePath);
                return PackageOperationResponse.CreateFailure($"SampleGenerator failed with validation errors: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError(ex, "Validation error while generating samples for package: {PackagePath}", packagePath);
                return PackageOperationResponse.CreateFailure($"SampleGenerator failed with validation errors: {ex.Message}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while generating samples for package: {PackagePath}", packagePath);
                return PackageOperationResponse.CreateFailure($"SampleGenerator threw an exception: {ex.Message}");
            }
        }

        private async Task<(int SamplesCount, string OutputDirectory, string Language, string FileNames, PackageInfo PackageInfo)> GenerateSamplesInternalAsync(
            string prompt,
            string packagePath,
            bool overwrite,
            string? model,
            string[]? extraContextPaths,
            CancellationToken ct)
        {
            var languageService = await GetLanguageServiceAsync(packagePath, ct)
                ?? throw new ArgumentException("Unable to determine language for package (resolver returned null). Ensure repository structure and Language-Settings.ps1 are correct.");

            var packageInfo = await languageService.GetPackageInfo(packagePath, ct);
            var resolvedOutputDirectory = packageInfo.SamplesDirectory;

            SampleLanguageContext sampleContext = languageService.SampleLanguageContext;
            var language = sampleContext.Language;

            logger.LogInformation("Starting sample generation with prompt: {prompt}", prompt);
            logger.LogDebug("Package path: {packagePath}, Language: {language}, Output: {outputDirectory}",
                packageInfo.PackagePath, language, resolvedOutputDirectory);

            var allPaths = new List<string> { packageInfo.PackagePath };
            if (extraContextPaths != null && extraContextPaths.Length > 0)
            {
                foreach (var extraContextPath in extraContextPaths)
                {
                    if (!string.IsNullOrWhiteSpace(extraContextPath))
                    {
                        var fullPath = Path.GetFullPath(extraContextPath.Trim());
                        allPaths.Add(fullPath);
                        logger.LogDebug("Will include extra context from path: {path}", fullPath);
                    }
                }
            }

            var context = await sampleContext.LoadContextAsync(
                allPaths,
                SampleConstants.MaxContextCharacters,
                SampleConstants.MaxCharactersPerFile,
                ct);

            logger.LogDebug("Loaded context: {length} characters", context.Length);

            if (string.IsNullOrWhiteSpace(context))
            {
                throw new InvalidOperationException(
                    $"No source code content could be loaded from the package path '{packageInfo.PackagePath}'. " +
                    $"Please verify the path contains valid source files for language '{language}'.");
            }

            var languageInstructions = sampleContext.GetSampleGenerationInstructions();

            var enhancedPrompt = $@"
Generate samples for the {packageInfo.PackageName} client library in {language} with the following guidelines:
- IMPORTANT: Create a SEPARATE sample file for each distinct scenario
{languageInstructions}

Scenarios description:
{prompt}

<context>
{context}
</context>

";
            logger.LogDebug("Enhanced prompt prepared with {contextLength} characters of context", context.Length);

            var microagent = string.IsNullOrEmpty(model)
                ? new Microagent<List<GeneratedSample>>() { Instructions = enhancedPrompt }
                : new Microagent<List<GeneratedSample>>() { Instructions = enhancedPrompt, Model = model };

            logger.LogInformation("Calling microagent service...");
            var samples = await _microagentHostService.RunAgentToCompletion(microagent, ct);
            logger.LogInformation("Microagent service returned");
            logger.LogDebug("Microagent completed, returned {sampleCount} samples", samples?.Count ?? 0);

            var writtenSamples = new List<GeneratedSample>();

            if (samples == null || samples.Count == 0)
            {
                logger.LogWarning("Microagent returned no samples");
            }
            else
            {
                logger.LogInformation("Generated {sampleCount} samples", samples.Count);
                Directory.CreateDirectory(resolvedOutputDirectory);

                foreach (var sample in samples)
                {
                    if (sample == null)
                    {
                        logger.LogWarning("Skipping null sample");
                        continue;
                    }

                    if (string.IsNullOrEmpty(sample.FileName))
                    {
                        logger.LogWarning("Skipping sample with null or empty filename");
                        continue;
                    }

                    if (string.IsNullOrEmpty(sample.Content))
                    {
                        logger.LogWarning("Skipping sample with null or empty content: {fileName}", sample.FileName);
                        continue;
                    }

                    var fileExtension = sampleContext.FileExtension;
                    var cleanFileName = Path.GetFileNameWithoutExtension(
                        sample.FileName.Replace('/', '_').Replace('\\', '_'));
                    var sampleFileName = $"{cleanFileName}{fileExtension}";

                    logger.LogDebug("Writing sample: {sampleFileName}", sampleFileName);
                    var sampleFilePath = Path.Combine(resolvedOutputDirectory, sampleFileName);

                    if (File.Exists(sampleFilePath) && !overwrite)
                    {
                        logger.LogWarning("Sample file already exists: {filePath}. Use --overwrite to replace it.",
                            sampleFilePath);
                    }
                    else
                    {
                        await File.WriteAllTextAsync(sampleFilePath, sample.Content, ct);
                        logger.LogInformation("Sample written to: {filePath}", sampleFilePath);
                        writtenSamples.Add(sample);
                    }
                }
            }

            logger.LogInformation("Sample generation completed");
            var fileNames = string.Join(", ", writtenSamples.Select(s => s.FileName));
            return (writtenSamples.Count, resolvedOutputDirectory, language, fileNames, packageInfo);
        }

        private async Task<string> ResolvePromptAsync(string rawPrompt, CancellationToken ct)
        {
            // If the raw prompt looks like a path to a markdown file, load its content
            if (!string.IsNullOrWhiteSpace(rawPrompt))
            {
                var trimmed = rawPrompt.Trim();
                if (trimmed.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase))
                {
                    var fullPath = Path.GetFullPath(trimmed);
                    if (File.Exists(fullPath))
                    {
                        logger.LogInformation("Loading prompt content from file: {promptFile}", fullPath);
                        var content = await File.ReadAllTextAsync(fullPath, ct);

                        // Expand any relative file links in the loaded prompt
                        content = await PromptHelper.ExpandRelativeFileLinksAsync(
                            content,
                            Path.GetDirectoryName(fullPath)!,
                            logger,
                            ct);
                        return content;
                    }
                }
            }

            // For direct text prompts, expand any relative file links using current directory
            return await PromptHelper.ExpandRelativeFileLinksAsync(
                rawPrompt,
                Directory.GetCurrentDirectory(),
                logger,
                ct);
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
                    logger.LogError("SampleGenerator failed: {error}", errorSummary);
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
