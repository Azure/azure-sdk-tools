// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.SampleGeneration;
using Azure.Sdk.Tools.Cli.Services;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    /// <summary>
    /// Represents a generated sample with its filename and content.
    /// </summary>
    /// <param name="FileName">The suggested filename for the sample</param>
    /// <param name="Content">The complete source code content of the sample</param>
    public record GeneratedSample(string FileName, string Content);

    [McpServerToolType, Description("Generates sample files")]
    public class SampleGeneratorTool : MCPTool
    {
        // Dependencies
        private readonly ILogger<SampleGeneratorTool> logger;
        private readonly IMicroagentHostService microagentHostService;
        private readonly IOutputHelper output;
        private readonly ILanguageSpecificCheckResolver languageResolver;
        private readonly IDockerService dockerService;

        public SampleGeneratorTool(
            IMicroagentHostService microagentHostService,
            ILogger<SampleGeneratorTool> logger,
            IOutputHelper output,
            ILanguageSpecificCheckResolver languageResolver,
            IDockerService dockerService
        ) : base()
        {
            this.microagentHostService = microagentHostService;
            this.logger = logger;
            this.output = output;
            this.languageResolver = languageResolver;
            this.dockerService = dockerService;

            CommandHierarchy = [
                SharedCommandGroups.Generators
            ];
        }

        private readonly Option<string> promptOption = new(
            name: "--prompt",
            getDefaultValue: () => "Generate a sample",
            description: "Prompt to use for the sample generation. It is either a path to a .md file containing the sample description, or a text prompt")
        {
            IsRequired = true,
        };

        private readonly Option<string> packagePathOption = new(
            name: "--package-path",
            getDefaultValue: () => ".",
            description: "Path to a module, underneath the 'sdk' folder for your repository (ex: /yoursource/azure-sdk-for-js/sdk/openai/openai)")
        {
            IsRequired = false,
        };

        private readonly Option<string> languageOption = new(
            name: "--language",
            description: "`dotnet|java|typescript|python|go` (default: auto-detect from repo)")
        {
            IsRequired = false,
        };

        private readonly Option<string> outputDirectoryOption = new(
            name: "--output-directory",
            description: "Root where samples will be written (default: language-appropriate folder)")
        {
            IsRequired = false,
        };

        private readonly Option<bool> verifyOption = new(
            name: "--verify",
            getDefaultValue: () => false,
            description: "Verify that the generated sample builds. Defaults to false")
        {
            IsRequired = false,
        };

        private readonly Option<bool> overwriteOption = new(
            name: "--overwrite",
            getDefaultValue: () => false,
            description: "Overwrite existing files without prompting")
        {
            IsRequired = false,
        };

        private readonly Option<string> modelOption = new(
            name: "--model",
            getDefaultValue: () => "gpt-4.1",
            description: "Azure OpenAI deployment name to use (default: `gpt-4.1`)")
        {
            IsRequired = false,
        };

        protected override Command GetCommand()
        {
            Command command = new("samples", "Generates sample files");
            command.AddOption(promptOption);
            command.AddOption(packagePathOption);
            command.AddOption(languageOption);
            command.AddOption(outputDirectoryOption);
            command.AddOption(verifyOption);
            command.AddOption(overwriteOption);
            command.AddOption(modelOption);
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            return command;
        }

        public override async Task<CommandResponse> HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            string prompt = ctx.ParseResult.GetValueForOption(promptOption) ?? "";
            string packagePath = ctx.ParseResult.GetValueForOption(packagePathOption) ?? ".";
            string? languageFromOption = ctx.ParseResult.GetValueForOption(languageOption);
            string? outputDirectoryFromOption = ctx.ParseResult.GetValueForOption(outputDirectoryOption);
            bool verify = ctx.ParseResult.GetValueForOption(verifyOption);
            bool overwrite = ctx.ParseResult.GetValueForOption(overwriteOption);
            string model = ctx.ParseResult.GetValueForOption(modelOption) ?? "gpt-4.1";

            try
            {
                await GenerateSampleAsync(prompt, packagePath, languageFromOption, outputDirectoryFromOption, verify, overwrite, model);
                return new DefaultCommandResponse { Message = "Sample generation completed successfully" };
            }
            catch (ArgumentException ex)
            {
                output.Output($"SampleGenerator failed with validation errors: {ex.Message}");
                return new DefaultCommandResponse { ResponseError = $"SampleGenerator failed with validation errors: {ex.Message}" };
            }
            catch (Exception ex)
            {
                output.Output($"SampleGenerator threw an exception: {ex.Message}");
                return new DefaultCommandResponse { ResponseError = $"SampleGenerator threw an exception: {ex.Message}" };
            }
        }

        [McpServerTool(Name = "azsdk_package_sample_generator"), Description("Generates sample files")]
        public DefaultCommandResponse GenerateSample(string prompt, string packagePath, string language, string outputDirectory, bool verify, bool overwrite, string model)
        {
            try
            {
                GenerateSampleAsync(prompt, packagePath, language, outputDirectory, verify, overwrite, model).Wait();
                return new DefaultCommandResponse() { Message = $"Sample generation completed" };
            }
            catch (ArgumentException ex)
            {
                return new DefaultCommandResponse() { ResponseError = $"SampleGenerator failed with validation errors: {ex.Message}" };
            }
            catch (Exception ex)
            {
                return new DefaultCommandResponse() { ResponseError = $"SampleGenerator threw an exception: {ex.Message}" };
            }
        }

        private async Task GenerateSampleAsync(string prompt, string packagePath, string? language, string? outputDirectory, bool verify, bool overwrite, string model)
        {
            var packageInfo = new FileHelper.PackageInfo(packagePath, languageResolver);

            var resolvedLanguage = language ?? packageInfo.Language;
            var resolvedOutputDirectory = outputDirectory ?? packageInfo.GetSamplesDirectory();

            logger.LogInformation("Starting sample generation with prompt: {prompt}", prompt);
            logger.LogDebug("Package path: {packagePath}, Language: {language}, Output: {outputDirectory}", packageInfo.PackagePath, resolvedLanguage, resolvedOutputDirectory);
            logger.LogDebug("Package info: {packageName} at {repoRoot}", packageInfo.PackageName, packageInfo.RepoRoot);
            logger.LogDebug("Detected language: {detectedLanguage}, Using language: {language}", packageInfo.Language, resolvedLanguage);
            logger.LogDebug("Default samples directory: {defaultSamplesDir}, Using output directory: {outputDirectory}", packageInfo.GetSamplesDirectory(), resolvedOutputDirectory);

            logger.LogDebug("Loading source code context from {packagePath}", packageInfo.PackagePath);
            var sourceContext = await GetSourceContextAsync(packageInfo.PackagePath, resolvedLanguage, maxBudget: 3000000, priorityBudget: 8000);
            logger.LogDebug("Loaded source context: {length} characters", sourceContext.Length);

            if (string.IsNullOrWhiteSpace(sourceContext))
            {
                throw new InvalidOperationException($"No source code content could be loaded from the package path '{packageInfo.PackagePath}'. Please verify the path contains valid source files for language '{resolvedLanguage}'.");
            }

            var languageInstructions = LanguageSupport.GetInstructions(resolvedLanguage);
            logger.LogDebug("Using language-specific instructions for: {language}", resolvedLanguage);

            var enhancedPrompt = $@"
Generate samples for the {packageInfo.PackageName} client library in {resolvedLanguage} with the following guidelines:
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

            var microagent = new Microagent<List<GeneratedSample>>()
            {
                Instructions = enhancedPrompt,
                Model = model
            };

            try
            {
                logger.LogInformation("Calling microagent service...");
                var samples = await microagentHostService.RunAgentToCompletion(microagent);
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
                        var processedSample = await ProcessAndVerifySampleAsync(
                            sample,
                            verify,
                            resolvedLanguage,
                            packageInfo.PackageName);

                        if (processedSample == null)
                        {
                            continue; // Sample was invalid and skipped
                        }

                        var fileExtension = LanguageSupport.GetFileExtension(resolvedLanguage);

                        var cleanFileName = Path.GetFileNameWithoutExtension(processedSample.FileName.Replace('/', '_').Replace('\\', '_'));
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
                            await File.WriteAllTextAsync(sampleFilePath, processedSample.Content);
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

        private async Task<string> GetSourceContextAsync(string packagePath, string language, int maxBudget = 3000000, int priorityBudget = 8000)
        {
            static int IsClientLikeName(FileHelper.FileMetadata file)
            {
                var fileName = Path.GetFileNameWithoutExtension(file.FilePath).ToLowerInvariant();
                return fileName.Contains("client") ||
                       fileName.Contains("service") ||
                       fileName.EndsWith("client") ||
                       fileName.EndsWith("service") ? 1 : 10;
            }

            var sourceInputs = LanguageSourceContextProvider.CreateSourceInputs(packagePath, language);

            var result = await FileHelper.LoadFilesAsync(
                inputs: sourceInputs,
                relativeTo: packagePath,
                totalBudget: maxBudget,
                perFileLimit: priorityBudget,
                priorityFunc: IsClientLikeName,
                logger: logger
            );

            return result;
        }

        private List<FileHelper.SourceInput> CreateSourceInputsForLanguage(string packagePath, string language)
        {
            var sourceInputs = new List<FileHelper.SourceInput>();

            switch (language.ToLowerInvariant())
            {
                case "dotnet":
                    sourceInputs.Add(new FileHelper.SourceInput(
                        Path: Path.Combine(packagePath, "src"),
                        IncludeExtensions: [".cs"]
                    ));

                    var testsPath = Path.Combine(packagePath, "tests");
                    if (Directory.Exists(testsPath))
                    {
                        var testEnvFiles = Directory.GetFiles(testsPath, "*TestEnvironment.cs", SearchOption.AllDirectories);
                        foreach (var testEnvFile in testEnvFiles)
                        {
                            sourceInputs.Add(new FileHelper.SourceInput(
                                Path: testEnvFile
                            ));
                        }
                    }

                    var samplesPath = Path.Combine(packagePath, "tests", "samples");
                    if (Directory.Exists(samplesPath))
                    {
                        sourceInputs.Add(new FileHelper.SourceInput(
                            Path: samplesPath,
                            IncludeExtensions: [".cs"]
                        ));
                    }

                    break;

                case "java":
                    sourceInputs.Add(new FileHelper.SourceInput(
                        Path: packagePath,
                        IncludeExtensions: [".java"],
                        ExcludeGlobPatterns: [
                            "**/target/**",
                            "**/.gradle/**",
                            "**/build/**",
                            "**/.idea/**",
                            "**/out/**"
                        ]
                    ));
                    break;

                case "typescript":
                    var distEsmPath = Path.Combine(packagePath, "dist", "esm");
                    var srcPath = Path.Combine(packagePath, "src");
                    if (Directory.Exists(distEsmPath))
                    {
                        sourceInputs.Add(new FileHelper.SourceInput(
                            Path: distEsmPath,
                            IncludeExtensions: [".d.ts"]
                        ));
                    }
                    else if (Directory.Exists(srcPath))
                    {
                        sourceInputs.Add(new FileHelper.SourceInput(
                            Path: srcPath,
                            IncludeExtensions: [".ts"],
                            ExcludeGlobPatterns: [
                                "**/node_modules/**",
                                "**/*.min.js",
                                "**/*.bundle.js",
                                "**/*.map"
                            ]
                        ));
                    }
                    else
                    {
                        throw new InvalidOperationException($"No valid TypeScript source directories found in package path '{packagePath}'. Expected 'dist/esm' or 'src'.");
                    }

                    var sampleEnvPath = Path.Combine(packagePath, "sample.env");
                    if (File.Exists(sampleEnvPath))
                    {
                        sourceInputs.Add(new FileHelper.SourceInput(
                            Path: sampleEnvPath
                        ));
                    }

                    var samplesDevPath = Path.Combine(packagePath, "samples-dev");
                    if (Directory.Exists(samplesDevPath))
                    {
                        sourceInputs.Add(new FileHelper.SourceInput(
                            Path: samplesDevPath,
                            IncludeExtensions: [".ts"]
                        ));
                    }

                    var testSnippetsPath = Path.Combine(packagePath, "test", "snippets.spec.ts");
                    if (File.Exists(testSnippetsPath))
                    {
                        sourceInputs.Add(new FileHelper.SourceInput(
                            Path: testSnippetsPath
                        ));
                    }
                    break;

                case "python":
                    sourceInputs.Add(new FileHelper.SourceInput(
                        Path: packagePath,
                        IncludeExtensions: [".py"],
                        ExcludeGlobPatterns: [
                            "**/__pycache__/**",
                            "**/build/**",
                            "**/dist/**",
                            "**/.pytest_cache/**",
                            "**/venv/**",
                            "**/env/**",
                            "**/.venv/**"
                        ]
                    ));
                    break;

                case "go":
                    sourceInputs.Add(new FileHelper.SourceInput(
                        Path: packagePath,
                        IncludeExtensions: [".go"],
                        ExcludeGlobPatterns: [
                            "**/vendor/**",
                            "**/bin/**"
                        ]
                    ));
                    break;

                default:
                    throw new ArgumentException($"Unsupported language for source context loading: '{language}'. Supported languages are: dotnet, java, typescript, javascript, python, go", nameof(language));
            }

            return sourceInputs;
        }

        /// <summary>
        /// Validates and optionally verifies a generated sample.
        /// </summary>
        /// <param name="sample">The sample to process</param>
        /// <param name="verify">Whether to run verification on the sample</param>
        /// <param name="language">The programming language of the sample</param>
        /// <param name="packageName">The package name to exclude from dependency discovery</param>
        /// <returns>The processed sample, or null if the sample was invalid and should be skipped</returns>
        private async Task<GeneratedSample?> ProcessAndVerifySampleAsync(
            GeneratedSample sample,
            bool verify,
            string language,
            string? packageName)
        {
            // Validate sample before processing
            if (sample == null)
            {
                logger.LogWarning("Skipping null sample");
                return null;
            }

            if (string.IsNullOrEmpty(sample.FileName))
            {
                logger.LogWarning("Skipping sample with null or empty filename");
                return null;
            }

            if (string.IsNullOrEmpty(sample.Content))
            {
                logger.LogWarning("Skipping sample with null or empty content: {fileName}", sample.FileName);
                return null;
            }

            GeneratedSample finalSample = sample;

            // Verify the sample if verification is enabled
            if (verify)
            {
                logger.LogInformation("Verifying sample: {fileName}", sample.FileName);

                try
                {
                    var verificationResult = await SampleVerification.VerifyAndFixSampleAsync(
                        sample,
                        language,
                        dockerService,
                        microagentHostService,
                        logger,
                        null, // clientDist
                        packageName,
                        CancellationToken.None);

                    finalSample = sample with { Content = verificationResult.Content };

                    if (verificationResult.Succeeded)
                    {
                        logger.LogInformation("Sample verification succeeded after {attempts} attempts for {fileName}",
                            verificationResult.AttemptsMade, sample.FileName);
                    }
                    else
                    {
                        logger.LogWarning("Sample verification failed after {attempts} attempts for {fileName}",
                            verificationResult.AttemptsMade, sample.FileName);

                        // Log the last attempt's errors for debugging
                        var lastAttempt = verificationResult.Attempts.LastOrDefault();
                        if (lastAttempt != null)
                        {
                            logger.LogDebug("Last verification error for {fileName}: {error}",
                                sample.FileName, lastAttempt.TypeCheckOutput);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during sample verification for {fileName}", sample.FileName);
                    // Continue with unverified sample
                }
            }

            return finalSample;
        }
    }

}
