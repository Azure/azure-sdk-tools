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

        private readonly Option<bool> checkOption = new("--check")
        {
            Description = "Validate the ownership YAML and report whether CODEOWNERS is up to date without writing it",
            Required = false,
            DefaultValueFactory = _ => false,
        };

        // Export section command options
        private readonly Option<string> codeownersPathOption = new("--codeowners-path")
        {
            Description = "Path to the CODEOWNERS file",
            Required = true,
        };

        private readonly Option<string[]> sectionsOption = new("--section")
        {
            Description = "Section name to export. Can be specified multiple times.",
            Required = true,
            AllowMultipleArgumentsPerToken = false,
        };

        private readonly Option<string> outputFilePathOption = new("--output-file")
        {
            Description = "File path to write exported content",
            Required = true,
        };

        // Check-package command options
        private readonly Option<string> directoryPathOption = new("--directory-path")
        {
            Description = "Relative path to the package directory from the repo root",
            Required = true,
        };

        // Audit command options
        private readonly Option<bool> fixOption = new("--fix")
        {
            Description = "Apply fixes for violations that support automated repair",
            Required = false,
            DefaultValueFactory = _ => false,
        };

        private readonly Option<bool> forceOption = new("--force")
        {
            Description = "Override safety thresholds (e.g., allow removing more than 5 invalid owners)",
            Required = false,
            DefaultValueFactory = _ => false,
        };

        // Command names
        private const string generateCodeownersCommandName = "generate";
        private const string exportSectionCommandName = "export-section";
        private const string checkPackageCommandName = "check-package";
        private const string updateCacheCommandName = "update-cache";
        private const string auditCommandName = "audit";

        // MCP Tool Names
        private const string CodeownerGenerateToolName = "azsdk_engsys_codeowner_generate";
        private const string CodeownerAuditToolName = "azsdk_engsys_codeowner_audit";
        private const string CodeownerCheckPackageToolName = "azsdk_engsys_codeowner_check_package";
        private const string CodeownerUpdateCacheToolName = "azsdk_engsys_codeowner_update_cache";

        /// <summary>
        /// Exit code for 'generate --check' when the ownership YAML is valid but the checked-in
        /// CODEOWNERS file is stale. Distinct from 1 so CI can let contributor PRs through and
        /// queue a regeneration instead of failing them.
        /// </summary>
        private const int StaleCodeownersExitCode = 2;

        private readonly ILogger<CodeownersTool> logger;
        private readonly ICodeownersGenerateHelper codeownersGenerateHelper;
        private readonly ICheckPackageHelper checkPackageHelper;
        private readonly ICodeownersAuditHelper codeownersAuditHelper;
        private readonly IGitHelper gitHelper;
        private readonly IDevOpsService devOpsService;

        public CodeownersTool(
            ILogger<CodeownersTool> logger,
            ILoggerFactory? loggerFactory,
            ICodeownersGenerateHelper codeownersGenerateHelper,
            ICheckPackageHelper checkPackageHelper,
            ICodeownersAuditHelper codeownersAuditHelper,
            IGitHelper gitHelper,
            IDevOpsService devOpsService
        )
        {
            this.logger = logger;
            this.codeownersGenerateHelper = codeownersGenerateHelper;
            this.checkPackageHelper = checkPackageHelper;
            this.codeownersAuditHelper = codeownersAuditHelper;
            this.gitHelper = gitHelper;
            this.devOpsService = devOpsService;

            CodeownersUtils.Utils.Log.Configure(loggerFactory);
        }

        protected override List<Command> GetCommands() =>
        [
            new(generateCodeownersCommandName, "Render .github/CODEOWNERS from the repository's ownership YAML")
            {
                repoRootOption, checkOption,
            },
            new(exportSectionCommandName, "Export one or more named sections from a CODEOWNERS file")
            {
                codeownersPathOption, sectionsOption, outputFilePathOption,
            },
            new(checkPackageCommandName, "Check that a package has sufficient owners, PR labels, and service owners")
            {
                directoryPathOption, repoRootOption, repoOption,
            },
            new McpCommand(updateCacheCommandName, "Run the CODEOWNERS cache update pipeline", CodeownerUpdateCacheToolName),
            new(auditCommandName, "Audit the repository's ownership YAML for violations and optionally fix them. You MUST update the CODEOWNERS cache before running this command.")
            {
                repoRootOption, fixOption, forceOption,
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
                        parseResult.GetValue(checkOption),
                        ct);

                case exportSectionCommandName:
                    return await ExportSection(
                        parseResult.GetValue(codeownersPathOption)!,
                        parseResult.GetValue(sectionsOption)!,
                        parseResult.GetValue(outputFilePathOption)!,
                        ct);

                case checkPackageCommandName:
                    return await CheckPackage(
                        parseResult.GetValue(directoryPathOption)!,
                        await gitHelper.DiscoverRepoRootAsync(parseResult.GetValue(repoRootOption), ct),
                        parseResult.GetValue(repoOption),
                        ct);

                case updateCacheCommandName:
                    return await UpdateCache(ct);

                case auditCommandName:
                    return await Audit(
                        await gitHelper.DiscoverRepoRootAsync(parseResult.GetValue(repoRootOption), ct),
                        parseResult.GetValue(fixOption),
                        parseResult.GetValue(forceOption),
                        ct);

                default:
                    return new DefaultCommandResponse { ResponseError = $"Unknown command: '{command}'" };
            }
        }

        [McpServerTool(Name = CodeownerGenerateToolName), Description("Render .github/CODEOWNERS from the repository's ownership YAML. Use check mode to verify the checked-in file is up to date without writing it.")]
        public async Task<DefaultCommandResponse> GenerateCodeowners(
            string repoRoot,
            bool check = false,
            CancellationToken ct = default)
        {
            try
            {
                var result = await codeownersGenerateHelper.Generate(repoRoot, check, ct);

                // Exit codes are a CI contract: 1 for invalid YAML, 2 for valid-but-stale CODEOWNERS.
                if (result.Errors.Count > 0)
                {
                    return new DefaultCommandResponse
                    {
                        ResponseError = string.Join(Environment.NewLine, result.Errors),
                    };
                }

                if (check && !result.IsUpToDate)
                {
                    return new DefaultCommandResponse
                    {
                        ExitCode = StaleCodeownersExitCode,
                        Message = $"{result.OutputPath} is out of date. Run 'azsdk config codeowners generate' to update it.",
                    };
                }

                return new DefaultCommandResponse
                {
                    Message = check
                        ? $"{result.OutputPath} is up to date."
                        : $"Wrote {result.OutputPath}.",
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to generate CODEOWNERS file");
                return new DefaultCommandResponse { ResponseError = $"Failed to generate CODEOWNERS file: {ex.Message}" };
            }
        }

        public async Task<DefaultCommandResponse> ExportSection(
            string codeownersPath,
            string[] sections,
            string output, CancellationToken ct)
        {
            if (!File.Exists(codeownersPath))
            {
                return new DefaultCommandResponse
                {
                    ResponseError = $"CODEOWNERS file not found: {codeownersPath}"
                };
            }

            var lines = (await File.ReadAllLinesAsync(codeownersPath, ct)).ToList();
            var exportedLines = new List<string>();

            foreach (var sectionName in sections)
            {
                var (headerStart, contentStart, sectionEnd) = CodeownersSectionFinder.FindSection(lines, sectionName);
                if (headerStart == -1)
                {
                    logger.LogError("Section '{SectionName}' not found in CODEOWNERS file", sectionName);
                    return new DefaultCommandResponse
                    {
                        ResponseError = $"Section '{sectionName}' not found in CODEOWNERS file"
                    };
                }

                exportedLines.AddRange(lines.GetRange(headerStart, sectionEnd - headerStart));
            }

            await File.WriteAllLinesAsync(output, exportedLines, ct);

            return new DefaultCommandResponse
            {
                Message = $"Exported {sections.Length} section(s) to {output}"
            };
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
        /// Audits the repository's ownership YAML for violations.
        /// The CODEOWNERS cache MUST be updated before running audit.
        /// </summary>
        [McpServerTool(Name = CodeownerAuditToolName), Description("Audit the repository's ownership YAML against cached org, team, and label data. Update the CODEOWNERS cache before calling this.")]
        public async Task<CommandResponse> Audit(string repoRoot, bool fix = false, bool force = false, CancellationToken ct = default)
        {
            try
            {
                return await codeownersAuditHelper.RunAudit(repoRoot, fix, force, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Audit failed");
                return new DefaultCommandResponse { ResponseError = $"Audit failed: {ex.Message}" };
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
