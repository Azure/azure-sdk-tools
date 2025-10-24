// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.SampleGeneration;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    /// <summary>
    /// Represents a generated sample with its filename and content.
    /// </summary>
    /// <param name="FileName">The suggested filename for the sample</param>
    /// <param name="Content">The complete source code content of the sample</param>
    public record GeneratedSample(string FileName, string Content);

    [McpServerToolType, Description("Generates sample files")]
    public class SampleGeneratorTool(
        IMicroagentHostService microagentHostService,
        ILogger<SampleGeneratorTool> logger,
        ILanguageSpecificResolver<IPackageInfoHelper> packageInfoResolver,
        ILanguageSpecificResolver<SampleLanguageContext> sampleContextResolver
    ) : MCPTool
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Generators];

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

        protected override Command GetCommand() => new("samples", "Generates sample files")
        {
            SharedOptions.PackagePath,
            promptOption,
            overwriteOption,
            modelOption
        };

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            // Retrieve raw prompt argument
            string rawPrompt = parseResult.GetValue(promptOption) ?? "";
            string prompt = rawPrompt;

            // If the raw prompt looks like a path to a markdown file (no newlines, ends with .md/.markdown, file exists), load its content.
            if (!string.IsNullOrWhiteSpace(rawPrompt)
                && rawPrompt.IndexOf('\n') == -1
                && rawPrompt.IndexOf('\r') == -1)
            {
                try
                {
                    var trimmed = rawPrompt.Trim();
                    if (trimmed.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || trimmed.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase))
                    {
                        var fullPath = Path.GetFullPath(trimmed);
                        if (File.Exists(fullPath))
                        {
                            logger.LogInformation("Loading prompt content from file: {promptFile}", fullPath);
                            prompt = await File.ReadAllTextAsync(fullPath, ct);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to read prompt file '{file}'. Falling back to raw prompt text.", rawPrompt);
                    prompt = rawPrompt; // fallback
                }
            }
            string packagePath = parseResult.GetValue(SharedOptions.PackagePath) ?? ".";
            bool overwrite = parseResult.GetValue(overwriteOption);
            var model = parseResult.GetValue(modelOption);

            try
            {
                await GenerateSampleAsync(prompt, packagePath, overwrite, model, ct);
                return new DefaultCommandResponse { Message = "Sample generation completed successfully" };
            }
            catch (ArgumentException ex)
            {
                logger.LogError(ex, "SampleGenerator failed with validation errors");
                return new DefaultCommandResponse { ResponseError = $"SampleGenerator failed with validation errors: {ex.Message}" };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SampleGenerator threw an exception");
                return new DefaultCommandResponse { ResponseError = $"SampleGenerator threw an exception: {ex.Message}" };
            }
        }

        private async Task GenerateSampleAsync(string prompt, string packagePath, bool overwrite, string model, CancellationToken ct)
        {
            IPackageInfoHelper? helper = await packageInfoResolver.Resolve(packagePath, ct) ?? throw new ArgumentException("Unable to determine language for package (resolver returned null). Ensure repository structure and Language-Settings.ps1 are correct.");
            var packageInfo = await helper.ResolvePackageInfo(packagePath, ct);
            var resolvedOutputDirectory = packageInfo.SamplesDirectory;

            logger.LogDebug("Loading source code context from {packagePath}", packageInfo.PackagePath);
            SampleLanguageContext sampleContext = await sampleContextResolver.Resolve(packagePath, ct) ?? throw new ArgumentException("Unable to determine language for package (resolver returned null). Ensure repository structure and Language-Settings.ps1 are correct.");
            var language = sampleContext.Language;

            logger.LogInformation("Starting sample generation with prompt: {prompt}", prompt);
            logger.LogDebug("Package path: {packagePath}, Language: {language}, Output: {outputDirectory}", packageInfo.PackagePath, language, resolvedOutputDirectory);
            logger.LogDebug("Package info: {packageName} at {repoRoot}", packageInfo.PackageName, packageInfo.RepoRoot);
            logger.LogDebug("Samples directory: {outputDirectory}", resolvedOutputDirectory);

            var sourceContext = await sampleContext.GetClientLibrarySourceCodeAsync(packageInfo.PackagePath, totalBudget: 3000000, perFileLimit: 50000, ct);
            logger.LogDebug("Loaded source context: {length} characters", sourceContext.Length);

            if (string.IsNullOrWhiteSpace(sourceContext))
            {
                throw new InvalidOperationException($"No source code content could be loaded from the package path '{packageInfo.PackagePath}'. Please verify the path contains valid source files for language '{language}'.");
            }

            var languageInstructions = sampleContext.GetSampleGenerationInstructions();

            var enhancedPrompt = $@"
Generate samples for the {packageInfo.PackageName} client library in {language} with the following guidelines:
- IMPORTANT: Create a SEPARATE sample file for each distinct scenario
{languageInstructions}

Scenarios description:
{prompt}

<source_context>
{sourceContext}
</source_context>

";
            logger.LogDebug("Enhanced prompt prepared with {contextLength} characters of source context", sourceContext.Length);

            logger.LogDebug("Starting microagent with model: {model}", model);
            logger.LogDebug("Enhanced prompt length: {promptLength} characters", enhancedPrompt.Length);

            var microagent = string.IsNullOrEmpty(model)
                ? new Microagent<List<GeneratedSample>>() { Instructions = enhancedPrompt }
                : new Microagent<List<GeneratedSample>>() { Instructions = enhancedPrompt, Model = model };

            try
            {
                logger.LogInformation("Calling microagent service...");
                var samples = await microagentHostService.RunAgentToCompletion(microagent, ct);
                logger.LogInformation("Microagent service returned");
                logger.LogDebug("Microagent completed, returned {sampleCount} samples", samples?.Count ?? 0);

                if (samples == null || samples.Count == 0)
                {
                    logger.LogWarning("Microagent returned no samples");
                    return;
                }
                else
                {
                    logger.LogInformation("Generated {sampleCount} samples", samples.Count);

                    logger.LogDebug("Output directory: {outputDirectory}", resolvedOutputDirectory);
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
                        var cleanFileName = Path.GetFileNameWithoutExtension(sample.FileName.Replace('/', '_').Replace('\\', '_'));
                        var sampleFileName = $"{cleanFileName}{fileExtension}";

                        logger.LogDebug("Writing sample: {sampleFileName}", sampleFileName);
                        var sampleFilePath = Path.Combine(resolvedOutputDirectory, sampleFileName);
                        logger.LogDebug("Full sample file path: {sampleFilePath}", sampleFilePath);

                        if (File.Exists(sampleFilePath) && !overwrite)
                        {
                            logger.LogWarning("Sample file already exists: {filePath}. Use --overwrite to replace it.", sampleFilePath);
                        }
                        else
                        {
                            await File.WriteAllTextAsync(sampleFilePath, sample.Content);
                            logger.LogInformation("Sample written to: {filePath}", sampleFilePath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error running microagent: {message}", ex.Message);
                throw;
            }

            logger.LogInformation("Sample generation completed");
        }
    }
}
