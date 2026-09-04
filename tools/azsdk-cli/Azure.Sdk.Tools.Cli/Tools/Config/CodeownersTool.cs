using System.ComponentModel;
using System.CommandLine;

using ModelContextProtocol.Server;

using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Helpers.Codeowners;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.CodeownersUtils.Utils;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.Config
{
    [Description("Validate and manipulate GitHub codeowners")]
    [McpServerToolType]
    public class CodeownersTool : MCPMultiCommandTool
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [
            SharedCommandGroups.Config,
            new CommandGroup("codeowners", "Validate and modify GitHub codeowners")
        ];

        private readonly Option<string> repoOption = new("--repo", "-r")
        {
            Description = "Repository name of the format <owner>/<repo> (e.g., Azure/azure-sdk-for-python).",
            Required = false,
        };

        private readonly Option<string> repoRootOption = new("--repo-root")
        {
            Description = "Path to the repository root (default: repo root of current directory)",
            Required = false,
            DefaultValueFactory = _ => ".",
        };

        private readonly Option<string> outputFilePathOption = new("--output-file")
        {
            Description = "Where to write the rendered CODEOWNERS file (default: configs.output from the owners config)",
            Required = false,
        };

        private readonly Option<bool> omitFallbackSectionsOption = new("--omit-fallback-sections")
        {
            Description = "Omit sections marked exclude-from-check-package from the rendered output",
            Required = false,
        };

        private readonly Option<string[]> fragmentPathsOption = new("--fragment")
        {
            Description = "Repo-relative path to an owners.yaml to lint. Repeatable; defaults to every fragment in the repo.",
            Required = false,
            AllowMultipleArgumentsPerToken = false,
        };

        private readonly Option<string> ownerOption = new("--owner")
        {
            Description = "GitHub alias (alice) or team alias (Azure/azure-sdk-write) to validate",
            Required = true,
        };

        // Check-package command options
        private readonly Option<string> directoryPathOption = new("--directory-path")
        {
            Description = "Relative path to the package directory from the repo root",
            Required = true,
        };

        // Command names
        private const string generateCodeownersCommandName = "generate";
        private const string checkPackageCommandName = "check-package";
        private const string updateCacheCommandName = "update-cache";
        private const string lintFragmentsCommandName = "lint-fragments";
        private const string validateOwnerCommandName = "validate-owner";

        // MCP Tool Names
        private const string CodeownerCheckPackageToolName = "azsdk_engsys_codeowner_check_package";
        private const string CodeownerUpdateCacheToolName = "azsdk_engsys_codeowner_update_cache";
        private const string CodeownerValidateOwnerToolName = "azsdk_engsys_codeowner_validate_owner";

        private readonly ILogger<CodeownersTool> logger;
        private readonly ICodeownersGenerateHelper codeownersGenerateHelper;
        private readonly ICheckPackageHelper checkPackageHelper;
        private readonly ICodeownersLintHelper codeownersLintHelper;
        private readonly IOwnerValidator ownerValidator;
        private readonly IGitHelper gitHelper;
        private readonly IDevOpsService devOpsService;

        public CodeownersTool(
            ILogger<CodeownersTool> logger,
            ILoggerFactory? loggerFactory,
            ICodeownersGenerateHelper codeownersGenerateHelper,
            ICheckPackageHelper checkPackageHelper,
            ICodeownersLintHelper codeownersLintHelper,
            IOwnerValidator ownerValidator,
            IGitHelper gitHelper,
            IDevOpsService devOpsService
        )
        {
            this.logger = logger;
            this.codeownersGenerateHelper = codeownersGenerateHelper;
            this.checkPackageHelper = checkPackageHelper;
            this.codeownersLintHelper = codeownersLintHelper;
            this.ownerValidator = ownerValidator;
            this.gitHelper = gitHelper;
            this.devOpsService = devOpsService;

            CodeownersUtils.Utils.Log.Configure(loggerFactory);
        }

        protected override List<Command> GetCommands() =>
        [
            new(generateCodeownersCommandName, "Render .github/CODEOWNERS from the repository's ownership YAML")
            {
                repoRootOption, omitFallbackSectionsOption, outputFilePathOption,
            },
            new(checkPackageCommandName, "Check that a package has sufficient owners, PR labels, and service owners")
            {
                directoryPathOption, repoRootOption, repoOption,
            },
            new McpCommand(updateCacheCommandName, "Run the CODEOWNERS cache update pipeline", CodeownerUpdateCacheToolName),
            new(lintFragmentsCommandName, "Check owners.yaml fragments for invalid owners, insufficient owners, and unknown labels. You MUST update the CODEOWNERS cache before running this command.")
            {
                repoRootOption, fragmentPathsOption,
            },
            new McpCommand(validateOwnerCommandName, "Check whether a GitHub alias or team can be a code owner", CodeownerValidateOwnerToolName)
            {
                ownerOption,
            },
        ];

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var command = parseResult.CommandResult.Command.Name;

            switch (command)
            {
                case generateCodeownersCommandName:
                    return await GenerateCodeowners(
                        await gitHelper.DiscoverRepoRootAsync(parseResult.GetValue(repoRootOption), ct),
                        parseResult.GetValue(omitFallbackSectionsOption),
                        parseResult.GetValue(outputFilePathOption),
                        ct);

                case checkPackageCommandName:
                    return await CheckPackage(
                        parseResult.GetValue(directoryPathOption)!,
                        await gitHelper.DiscoverRepoRootAsync(parseResult.GetValue(repoRootOption), ct),
                        parseResult.GetValue(repoOption),
                        ct);

                case updateCacheCommandName:
                    return await UpdateCache(ct);

                case lintFragmentsCommandName:
                    return await LintFragments(
                        await gitHelper.DiscoverRepoRootAsync(parseResult.GetValue(repoRootOption), ct),
                        parseResult.GetValue(fragmentPathsOption) ?? [],
                        ct);

                case validateOwnerCommandName:
                    return await ValidateOwner(parseResult.GetValue(ownerOption)!, ct);

                default:
                    return new DefaultCommandResponse { ResponseError = $"Unknown command: '{command}'" };
            }
        }

        /// <summary>
        /// Renders the CODEOWNERS file. Always writes: entries the caches will not stand behind are
        /// dropped and reported rather than failing the run, so the rendered file keeps up with the
        /// YAML instead of freezing whenever some ownership decays.
        /// </summary>
        public async Task<CommandResponse> GenerateCodeowners(
            string repoRoot,
            bool omitFallbackSections,
            string? outputPath,
            CancellationToken ct = default)
        {
            try
            {
                var result = await codeownersGenerateHelper.Generate(repoRoot, omitFallbackSections, outputPath, ct);

                return new CodeownersGenerateResponse
                {
                    OutputPath = result.OutputPath,
                    Dropped = result.Dropped,
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to generate CODEOWNERS file");
                return new DefaultCommandResponse { ResponseError = $"Failed to generate CODEOWNERS file: {ex.Message}" };
            }
        }

        /// <summary>
        /// Validates that a package has sufficient owners, PR labels, and service owners, resolved
        /// from the owners YAML fragment that covers the package directory.
        /// </summary>
        [McpServerTool(Name = CodeownerCheckPackageToolName), Description("Check that a package has sufficient owners, PR labels, and service owners.")]
        public async Task<CommandResponse> CheckPackage(
            string directoryPath,
            string repoRoot,
            string? repo = null,
            CancellationToken ct = default)
        {
            var packageName = CheckPackageHelper.ResolvePackageName(directoryPath);

            try
            {
                return await checkPackageHelper.CheckPackage(directoryPath, repoRoot, repo, ct);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "check-package failed unexpectedly");
                return CreateCheckPackageFailureResponse(
                    directoryPath,
                    packageName,
                    repo,
                    CheckPackageIssue.Codes.UnexpectedError,
                    ex.Message,
                    "Retry the command. If the failure persists, use the support channel returned in this response.");
            }
        }

        private const int UpdateCachePipelineDefinitionId = 5112;

        [McpServerTool(Name = CodeownerUpdateCacheToolName), Description("Run the CODEOWNERS cache update pipeline. Use this after making changes to ownership information to unblock releases or other pipelines.")]
        public async Task<DefaultCommandResponse> UpdateCache(CancellationToken ct = default)
        {
            try
            {
                var build = await devOpsService.RunPipelineAsync(UpdateCachePipelineDefinitionId, new Dictionary<string, string>(), ct: ct);
                var pipelineUrl = DevOpsService.GetPipelineUrl(build.Id);
                logger.LogInformation("Started CODEOWNERS cache update pipeline: {pipelineUrl}", pipelineUrl);
                return new DefaultCommandResponse
                {
                    Message = $"CODEOWNERS cache update pipeline started successfully. Build id: {build.Id}. Pipeline run: {pipelineUrl}"
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start CODEOWNERS cache update pipeline");
                return new DefaultCommandResponse
                {
                    ResponseError = $"Failed to start CODEOWNERS cache update pipeline: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Checks owners.yaml fragments. Each fragment is judged on its own, so this is safe to run
        /// against only the files a pull request touched.
        /// The CODEOWNERS cache MUST be updated before running it.
        /// </summary>
        public async Task<CommandResponse> LintFragments(
            string repoRoot,
            string[] fragmentPaths,
            CancellationToken ct = default)
        {
            try
            {
                var result = await codeownersLintHelper.Lint(repoRoot, fragmentPaths, ct);

                return new CodeownersLintResponse { Fragments = result.Fragments };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "lint-fragments failed");
                return new DefaultCommandResponse { ResponseError = $"lint-fragments failed: {ex.Message}" };
            }
        }

        /// <summary>
        /// Answers whether one alias or team can be a code owner, and what to do when it cannot.
        /// </summary>
        [McpServerTool(Name = CodeownerValidateOwnerToolName), Description("Check whether a GitHub alias or team alias is a valid CODEOWNERS owner, and report what must change if it is not.")]
        public async Task<CommandResponse> ValidateOwner(string owner, CancellationToken ct = default)
        {
            try
            {
                await ownerValidator.EnsureUsableAsync(ct);
                var violation = ownerValidator.Validate(owner, where: null);

                return new ValidateOwnerResponse
                {
                    Owner = owner,
                    Valid = violation == null,
                    Members = ownerValidator.ExpandToIndividuals([owner]),
                    Violation = violation,
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "validate-owner failed");
                return new DefaultCommandResponse { ResponseError = $"validate-owner failed: {ex.Message}" };
            }
        }

        private static CheckPackageResponse CreateCheckPackageFailureResponse(
            string directoryPath,
            string packageName,
            string? repo,
            string issueCode,
            string message,
            string nextStep)
        {
            var response = new CheckPackageResponse
            {
                DirectoryPath = directoryPath,
                PackageName = packageName,
                Repo = repo,
                ResponseError = message,
            };

            response.Issues.Add(new CheckPackageIssue
            {
                Code = issueCode,
                Message = message,
                NextStep = nextStep,
            });

            return response;
        }
    }
}
