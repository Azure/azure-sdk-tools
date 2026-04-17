using System.ComponentModel;
using System.CommandLine;
using System.Text.RegularExpressions;

using ModelContextProtocol.Server;
using Octokit;

using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.CodeownersUtils.Editing;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.CodeownersUtils.Utils;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Tools.Core;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;

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

        // Core command options
        private readonly Option<string> repoOption = new("--repo", "-r")
        {
            Description = "The repository name",
            Required = true,
        };

        private readonly Option<string> pathOptionOptional = new("--path", "-p")
        {
            Description = "The repository path to check/validate",
            Required = false,
        };

        private readonly Option<string> serviceLabelOption = new("--service-label")
        {
            Description = "The service label",
            Required = false,
        };

        // Generate command options
        private readonly Option<string> repoRootOption = new("--repo-root")
        {
            Description = "Path to the repository root (default: repo root of current directory)",
            Required = false,
            DefaultValueFactory = _ => ".",
        };

        private readonly Option<string[]> packageTypesOption = new("--package-types")
        {
            Description = "Package types to include (default: client)",
            Required = false,
            DefaultValueFactory = _ => ["client"],
            AllowMultipleArgumentsPerToken = true,
        };

        private readonly Option<string> sectionOption = new("--section")
        {
            Description = "Section name in CODEOWNERS file to update (default: Client Libraries)",
            Required = false,
            DefaultValueFactory = _ => "Client Libraries",
        };

        // Management command options
        private readonly Option<string> githubUserOption = new("--github-user")
        {
            Description = "GitHub alias.",
            Required = false,
        };

        private readonly Option<string[]> multipleGithubUserOption = new("--github-user")
        {
            Description = "GitHub alias(es). Can be specified multiple times.",
            AllowMultipleArgumentsPerToken = true,
        };

        private readonly Option<string[]> labelsOption = new("--label")
        {
            Description = "Label name(s). Can be specified multiple times.",
            AllowMultipleArgumentsPerToken = true,
        };

        private readonly Option<string> packageOption = new("--package")
        {
            Description = "Package name",
        };

        private readonly Option<string> pathOption = new("--path", "-p")
        {
            Description = "Repository path (e.g., sdk/formrecognizer/)",
            Required = false,
        };

        private readonly Option<string> optionalRepoOption = new("--repo", "-r")
        {
            Description = "Repository name of the format <owner>/<repo> (e.g., Azure/azure-sdk-for-python).",
            Required = false,
        };



        private readonly IGitHubService githubService;
        private readonly ILogger<CodeownersTool> logger;
        private readonly ICodeownersValidatorHelper codeownersValidatorHelper;
        private readonly ICodeownersGenerateHelper codeownersGenerateHelper;
        private readonly ICodeownersManagementHelper codeownersManagementHelper;
        private readonly ICheckPackageHelper checkPackageHelper;
        private readonly IGitHelper gitHelper;
        private readonly IDevOpsService devOpsService;

        // URL constants
        private const string azureWriteTeamsBlobUrl = "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/azure-sdk-write-teams-blob";

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

        private readonly Option<OwnerType> ownerTypeOption = new("--owner-type")
        {
            Description = "Owner type",
            Required = false,
        };

        // Check-package command options
        private readonly Option<string> directoryPathOption = new("--directory-path")
        {
            Description = "Relative path to the package directory from the repo root",
            Required = true,
        };

        private readonly Option<string> codeownersCacheOption = new("--codeowners-cache")
        {
            Description = "Local filesystem path to a rendered CODEOWNERS cache file (overrides --repo-derived URL)",
            Required = false,
        };

        // Command names
        private const string generateCodeownersCommandName = "generate";
        private const string viewCodeownersCommandName = "view";
        private const string exportSectionCommandName = "export-section";
        private const string addCodeownersToPackageCommandName = "add-package-owner";
        private const string addLabelToPackageCommandName = "add-package-label";
        private const string addLabelOwnerCommandName = "add-label-owner";
        private const string removeCodeownersToPackageCommandName = "remove-package-owner";
        private const string removeLabelToPackageCommandName = "remove-package-label";
        private const string removeLabelOwnerCommandName = "remove-label-owner";
        private const string checkPackageCommandName = "check-package";
        private const string updateCacheCommandName = "update-cache";


        // MCP Tool Names
        private const string CodeownerViewToolName = "azsdk_engsys_codeowner_view";
        private const string CodeownerAddPackageOwnerToolName = "azsdk_engsys_codeowner_add_package_owner";
        private const string CodeownerAddLabelToolName = "azsdk_engsys_codeowner_add_package_label";
        private const string CodeownerAddLabelOwnerToolName = "azsdk_engsys_codeowner_add_label_owner";
        private const string CodeownerRemovePackageOwnerToolName = "azsdk_engsys_codeowner_remove_package_owner";
        private const string CodeownerRemoveLabelToolName = "azsdk_engsys_codeowner_remove_package_label";
        private const string CodeownerRemoveLabelOwnerToolName = "azsdk_engsys_codeowner_remove_label_owner";
        private const string CodeownerCheckPackageToolName = "azsdk_engsys_codeowner_check_package";
        private const string CodeownerUpdateCacheToolName = "azsdk_engsys_codeowner_update_cache";

        public CodeownersTool(
            IGitHubService githubService,
            ILogger<CodeownersTool> logger,
            ILoggerFactory? loggerFactory,
            ICodeownersValidatorHelper codeownersValidator,
            ICodeownersGenerateHelper codeownersGenerateHelper,
            IGitHelper gitHelper,
            ICodeownersManagementHelper codeownersManagementHelper,
            ICheckPackageHelper checkPackageHelper,
            IDevOpsService devOpsService
        )
        {
            this.githubService = githubService;
            this.logger = logger;
            this.codeownersValidatorHelper = codeownersValidator;
            this.codeownersGenerateHelper = codeownersGenerateHelper;
            this.codeownersManagementHelper = codeownersManagementHelper;
            this.checkPackageHelper = checkPackageHelper;
            this.gitHelper = gitHelper;
            this.devOpsService = devOpsService;

            CodeownersUtils.Utils.Log.Configure(loggerFactory);
        }

        protected override List<Command> GetCommands() =>
        [
            new(generateCodeownersCommandName, "Generate CODEOWNERS file from Azure DevOps work items")
            {
                repoRootOption, packageTypesOption, sectionOption,
            },
            new(viewCodeownersCommandName, "View CODEOWNERS associations for a user, label, package, or path")
            {
                githubUserOption, labelsOption, packageOption, pathOption, optionalRepoOption,
            },
            new(addCodeownersToPackageCommandName, "Add source owner(s) to a package")
            {
                multipleGithubUserOption, packageOption, optionalRepoOption,
            },
            new(addLabelToPackageCommandName, "Add PR label(s) to a package")
            {
                labelsOption, packageOption, optionalRepoOption,
            },
            new(addLabelOwnerCommandName, "Add owner(s) to a label and optional path")
            {
                multipleGithubUserOption, labelsOption, pathOption, ownerTypeOption, optionalRepoOption, sectionOption,
            },
            new(removeCodeownersToPackageCommandName, "Remove source owner(s) from a package")
            {
                multipleGithubUserOption, packageOption, optionalRepoOption,
            },
            new(removeLabelToPackageCommandName, "Remove PR label(s) from a package")
            {
                labelsOption, packageOption, optionalRepoOption,
            },
            new(removeLabelOwnerCommandName, "Remove owner(s) from a label and optional path")
            {
                multipleGithubUserOption, labelsOption, pathOption, ownerTypeOption, optionalRepoOption, sectionOption,
            },
            new(exportSectionCommandName, "Export one or more named sections from a CODEOWNERS file")
            {
                codeownersPathOption, sectionsOption, outputFilePathOption,
            },
            new(checkPackageCommandName, "Check that a package has sufficient owners, PR labels, and service owners from a CODEOWNERS cache file")
            {
                directoryPathOption, optionalRepoOption, codeownersCacheOption,
            },
            new McpCommand(updateCacheCommandName, "Run the CODEOWNERS cache update pipeline", CodeownerUpdateCacheToolName),
        ];

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var command = parseResult.CommandResult.Command.Name;

            if (command == generateCodeownersCommandName)
            {
                var repoRoot = await gitHelper.DiscoverRepoRootAsync(
                    parseResult.GetValue(repoRootOption), ct
                );
                var packageTypes = parseResult.GetValue(packageTypesOption);
                var section = parseResult.GetValue(sectionOption);
                var generateResult = await GenerateCodeowners(repoRoot, packageTypes, section, ct);
                return generateResult;
            }

            if (command == viewCodeownersCommandName)
            {
                var user = parseResult.GetValue(githubUserOption);
                var labels = parseResult.GetValue(labelsOption);
                var package = parseResult.GetValue(packageOption);
                var path = parseResult.GetValue(pathOption);
                var repo = parseResult.GetValue(optionalRepoOption);
                return await ViewCodeowners(user, labels, package, path, repo, ct);
            }

            if (command == addCodeownersToPackageCommandName)
            {
                var users = parseResult.GetValue(multipleGithubUserOption);
                var package = parseResult.GetValue(packageOption);
                var repo = parseResult.GetValue(optionalRepoOption);
                return await AddPackageOwner(users!, package!, repo, ct);
            }

            if (command == addLabelToPackageCommandName)
            {
                var labels = parseResult.GetValue(labelsOption);
                var package = parseResult.GetValue(packageOption);
                var repo = parseResult.GetValue(optionalRepoOption);
                return await AddPackageLabel(labels!, package!, repo, ct);
            }

            if (command == addLabelOwnerCommandName)
            {
                var users = parseResult.GetValue(multipleGithubUserOption);
                var labels = parseResult.GetValue(labelsOption);
                var ownerType = parseResult.GetValue(ownerTypeOption);
                var path = parseResult.GetValue(pathOption);
                var repo = parseResult.GetValue(optionalRepoOption);
                var section = parseResult.GetValue(sectionOption);
                return await AddLabelOwner(users!, labels!, ownerType!, path, repo, section, ct);
            }

            if (command == removeCodeownersToPackageCommandName)
            {
                var users = parseResult.GetValue(multipleGithubUserOption);
                var package = parseResult.GetValue(packageOption);
                var repo = parseResult.GetValue(optionalRepoOption);
                return await RemovePackageOwner(users!, package!, repo, ct);
            }

            if (command == removeLabelToPackageCommandName)
            {
                var labels = parseResult.GetValue(labelsOption);
                var package = parseResult.GetValue(packageOption);
                var repo = parseResult.GetValue(optionalRepoOption);
                return await RemovePackageLabel(labels!, package!, repo, ct);
            }

            if (command == removeLabelOwnerCommandName)
            {
                var users = parseResult.GetValue(multipleGithubUserOption);
                var labels = parseResult.GetValue(labelsOption);
                var ownerType = parseResult.GetValue(ownerTypeOption);
                var path = parseResult.GetValue(pathOption);
                var repo = parseResult.GetValue(optionalRepoOption);
                var section = parseResult.GetValue(sectionOption);
                return await RemoveLabelOwner(users!, labels!, ownerType!, path, repo, section, ct);
            }

            if (command == exportSectionCommandName)
            {
                var codeownersPath = parseResult.GetValue(codeownersPathOption);
                var sections = parseResult.GetValue(sectionsOption);
                var output = parseResult.GetValue(outputFilePathOption);
                return await ExportSection(codeownersPath!, sections!, output!, ct);
            }

            if (command == checkPackageCommandName)
            {
                var directoryPath = parseResult.GetValue(directoryPathOption);
                var cachePath = parseResult.GetValue(codeownersCacheOption);
                var repo = parseResult.GetValue(optionalRepoOption);
                return await CheckPackage(directoryPath!, cachePath, repo, ct);
            }

            if (command == updateCacheCommandName)
            {
                return await UpdateCache(ct);
            }

            return new DefaultCommandResponse { ResponseError = $"Unknown command: '{command}'" };
        }

        /// <summary>
        /// Generates CODEOWNERS file from Azure DevOps work items.
        /// </summary>
        public async Task<DefaultCommandResponse> GenerateCodeowners(
            string repoRoot,
            string[] packageTypes,
            string section,
            CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(repoRoot))
                {
                    return new DefaultCommandResponse
                    {
                        ResponseError = "Repository root path is required"
                    };
                }

                if (!Directory.Exists(repoRoot))
                {
                    return new DefaultCommandResponse
                    {
                        ResponseError = $"Repository root not found: {repoRoot}"
                    };
                }

                var repo = await gitHelper.GetRepoFullNameAsync(repoRoot, ct: ct);

                var codeownersPath = Path.Combine(repoRoot, ".github", "CODEOWNERS");
                if (!File.Exists(codeownersPath))
                {
                    return new DefaultCommandResponse
                    {
                        ResponseError = $"CODEOWNERS file not found: {codeownersPath}"
                    };
                }

                await codeownersGenerateHelper.GenerateCodeowners(repoRoot, repo, packageTypes, section, ct);

                return new DefaultCommandResponse
                {
                    Message = $"CODEOWNERS file generated successfully to {codeownersPath}"
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to generate CODEOWNERS file");
                return new DefaultCommandResponse
                {
                    ResponseError = $"Failed to generate CODEOWNERS file: {ex.Message}"
                };
            }
        }

        [McpServerTool(Name = CodeownerViewToolName), Description("View CODEOWNERS associations for a user, label(s), package, or path. Exactly one axis (githubUser, label, package, or path) must be specified. Multiple labels are treated as AND.")]
        public async Task<CommandResponse> ViewCodeowners(
            string? githubUser = null,
            string[] labels = null,
            string? package = null,
            string? path = null,
            string? repo = null,
            CancellationToken ct = default
        )
        {
            try
            {
                var hasLabels = labels?.Length > 0;
                var axes = new[] { !string.IsNullOrEmpty(githubUser), hasLabels, !string.IsNullOrEmpty(package), !string.IsNullOrEmpty(path) }.Count(a => a);
                if (axes == 0)
                {
                    return new DefaultCommandResponse { ResponseError = "Exactly one of github user, label, package, or path must be specified." };
                }
                if (axes > 1)
                {
                    return new DefaultCommandResponse { ResponseError = "Only one of github user, label, package, or path can be specified at a time." };
                }

                if (!string.IsNullOrEmpty(githubUser))
                {
                    return await codeownersManagementHelper.GetViewByUser(githubUser, repo, ct);
                }
                if (hasLabels)
                {
                    return await codeownersManagementHelper.GetViewByLabel(labels, repo, ct);
                }
                if (!string.IsNullOrEmpty(package))
                {
                    return await codeownersManagementHelper.GetViewByPackage(package, repo, ct);
                }
                return await codeownersManagementHelper.GetViewByPath(path!, repo, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error viewing codeowners data");
                return new DefaultCommandResponse { ResponseError = ex.Message };
            }
        }

        /// <summary>
        /// Exports named sections from a CODEOWNERS file to an output file.
        /// </summary>
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

        private const string CacheBaseUrl = "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/cache";

        /// <summary>
        /// Validates that a package has sufficient owners, PR labels, and service owners
        /// by reading from a CODEOWNERS cache. Uses --codeowners-cache if specified,
        /// otherwise builds a blob URL from --repo (explicit or inferred from git remote).
        /// </summary>
        [McpServerTool(Name = CodeownerCheckPackageToolName), Description("Check that a package has sufficient owners, PR labels, and service owners from a CODEOWNERS cache file.")]
        public async Task<CommandResponse> CheckPackage(
            string directoryPath,
            string? codeownersCachePath = null,
            string? repo = null,
            CancellationToken ct = default)
        {
            try
            {
                string cacheSource;
                if (!string.IsNullOrEmpty(codeownersCachePath))
                {
                    if (!File.Exists(codeownersCachePath))
                    {
                        return new DefaultCommandResponse
                        {
                            ResponseError = $"CODEOWNERS cache file not found: {codeownersCachePath}"
                        };
                    }
                    cacheSource = codeownersCachePath;
                }
                else
                {
                    repo = await ResolveRepo(repo, ct);
                    // repo is "Azure/azure-sdk-for-net" → split to build URL
                    var parts = repo.Split('/');
                    if (parts.Length != 2)
                    {
                        return new DefaultCommandResponse
                        {
                            ResponseError = $"Invalid repo format '{repo}'. Expected '<owner>/<repo>'."
                        };
                    }
                    cacheSource = $"{CacheBaseUrl}/{parts[0].ToLowerInvariant()}/{parts[1]}/CODEOWNERS.cache";
                }

                var entries = CodeownersParser.ParseCodeownersFile(cacheSource);

                return checkPackageHelper.CheckPackage(directoryPath, entries);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "check-package failed");
                return new DefaultCommandResponse { ResponseError = ex.Message };
            }
        }

        [McpServerTool(Name = CodeownerAddPackageOwnerToolName), Description("Add source owner(s) to a package in CODEOWNERS work items.")]
        public async Task<CommandResponse> AddPackageOwner(
            string[] githubUsers,
            string package,
            string? repo = null,
            CancellationToken ct = default
        )
        {
            try
            {
                repo = await ResolveRepo(repo, ct);
                return await codeownersManagementHelper.AddOwnersToPackage(
                    await FindOrCreateOwnerWorkItems(githubUsers, ct),
                    package,
                    repo,
                    ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error adding package owner(s)");
                return new DefaultCommandResponse { ResponseError = ex.Message };
            }
        }

        [McpServerTool(Name = CodeownerAddLabelToolName), Description("Add PR label(s) to a package in CODEOWNERS work items.")]
        public async Task<CommandResponse> AddPackageLabel(
            string[] labels,
            string package,
            string? repo = null,
            CancellationToken ct = default
        )
        {
            try
            {
                repo = await ResolveRepo(repo, ct);
                return await codeownersManagementHelper.AddLabelsToPackage(
                    await FindLabels(labels, ct),
                    package,
                    repo,
                    ct
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error adding package label(s)");
                return new DefaultCommandResponse { ResponseError = ex.Message };
            }
        }

        [McpServerTool(Name = CodeownerAddLabelOwnerToolName), Description("Add owner(s) to a label with an optional path in CODEOWNERS work items. Valid ownerType values: service-owner, azsdk-owner, pr-label.")]
        public async Task<CommandResponse> AddLabelOwner(
            string[] githubUsers,
            string[] labels,
            OwnerType ownerType,
            string? path = null,
            string? repo = null,
            string section = "Client Libraries",
            CancellationToken ct = default
        )
        {
            try
            {
                if (string.IsNullOrEmpty(section))
                {
                    throw new ArgumentException("Section name must be provided", nameof(section));
                }
                repo = await ResolveRepo(repo, ct);
                return await codeownersManagementHelper.AddOwnersAndLabelsToPath(
                    await FindOrCreateOwnerWorkItems(githubUsers, ct),
                    await FindLabels(labels, ct),
                    repo,
                    path,
                    ownerType,
                    section,
                    ct
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error adding label owner(s)");
                return new DefaultCommandResponse { ResponseError = ex.Message };
            }
        }

        [McpServerTool(Name = CodeownerRemovePackageOwnerToolName), Description("Remove source owner(s) from a package in CODEOWNERS work items.")]
        public async Task<CommandResponse> RemovePackageOwner(
            string[] githubUsers,
            string package,
            string? repo = null,
            CancellationToken ct = default
        )
        {
            try
            {
                repo = await ResolveRepo(repo, ct);
                return await codeownersManagementHelper.RemoveOwnersFromPackage(
                    await GetOwnerWorkItems(githubUsers, ct),
                    package,
                    repo,
                    ct
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error removing package owner(s)");
                return new DefaultCommandResponse { ResponseError = ex.Message };
            }
        }

        [McpServerTool(Name = CodeownerRemoveLabelToolName), Description("Remove PR label(s) from a package in CODEOWNERS work items.")]
        public async Task<CommandResponse> RemovePackageLabel(
            string[] labels,
            string package,
            string? repo = null,
            CancellationToken ct = default
        )
        {
            try
            {
                repo = await ResolveRepo(repo, ct);
                return await codeownersManagementHelper.RemoveLabelsFromPackage(
                    await FindLabels(labels, ct),
                    package,
                    repo,
                    ct
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error removing package label(s)");
                return new DefaultCommandResponse { ResponseError = ex.Message };
            }
        }

        [McpServerTool(Name = CodeownerRemoveLabelOwnerToolName), Description("Remove owner(s) from a label with an optional path in CODEOWNERS work items. Valid ownerType values: service-owner, azsdk-owner, pr-label.")]
        public async Task<CommandResponse> RemoveLabelOwner(
            string[] githubUsers,
            string[] labels,
            OwnerType ownerType,
            string? path = null,
            string? repo = null,
            string section = "Client Libraries",
            CancellationToken ct = default
        )
        {
            try
            {
                if (string.IsNullOrEmpty(section))
                {
                    throw new ArgumentException("Section name must be provided", nameof(section));
                }
                repo = await ResolveRepo(repo, ct);
                return await codeownersManagementHelper.RemoveOwnersFromLabelsAndPath(
                    await GetOwnerWorkItems(githubUsers, ct),
                    await FindLabels(labels, ct),
                    repo,
                    path,
                    ownerType,
                    section,
                    ct
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error removing label owner(s)");
                return new DefaultCommandResponse { ResponseError = ex.Message };
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

        private async Task<string> ResolveRepo(string? repo, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(repo))
            {
                repo = await gitHelper.GetRepoFullNameAsync(
                    await gitHelper.DiscoverRepoRootAsync(".", ct),
                    ct: ct
                );
                if (string.IsNullOrEmpty(repo))
                {
                    throw new InvalidOperationException("Could not infer repository. Use repo to specify it.");
                }
            }
            return repo;
        }

        private async Task<OwnerWorkItem[]> GetOwnerWorkItems(string[] ownerAliases, CancellationToken ct)
        {
            var ownerWorkItems = new List<OwnerWorkItem>();
            foreach (var alias in ownerAliases)
            {
                var ownerWorkItem = await codeownersManagementHelper.FindOwnerByGitHubAlias(alias, ct);
                if (ownerWorkItem == null)
                {
                    throw new Exception($"GitHub alias '{alias}' does not have a corresponding Owner work item in Azure DevOps.");
                }
                ownerWorkItems.Add(ownerWorkItem);
            }
            return ownerWorkItems.ToArray();
        }

        private async Task<OwnerWorkItem[]> FindOrCreateOwnerWorkItems(string[] ownerAliases, CancellationToken ct)
        {
            var ownerWorkItems = new List<OwnerWorkItem>();
            foreach (var alias in ownerAliases)
            {
                var isTeamAlias = IsTeamAlias(alias);
                if (isTeamAlias)
                {
                    await codeownersManagementHelper.ThrowIfInvalidTeamAlias(alias, ct);
                }

                // Owner work items exist for both individual and teams
                var existing = await codeownersManagementHelper.FindOwnerByGitHubAlias(alias, ct);
                if (existing != null)
                {
                    ownerWorkItems.Add(existing);
                    continue;
                }

                if (!isTeamAlias)
                {
                    var validation = await codeownersValidatorHelper.ValidateCodeOwnerAsync(alias, verbose: false, ct: ct);
                    if (!validation.IsValidCodeOwner)
                    {
                        throw new InvalidOperationException(
                            $"GitHub user '{alias}' is not a valid Azure SDK code owner: {validation.Message}");
                    }
                }

                var ownerWi = new OwnerWorkItem { GitHubAlias = alias };
                var created = await devOpsService.CreateWorkItemAsync(ownerWi, "Owner", alias, ct: ct);
                ownerWorkItems.Add(WorkItemMappers.MapToOwnerWorkItem(created));
            }
            return ownerWorkItems.ToArray();
        }

        private async Task<LabelWorkItem[]> FindLabels(string[] labels, CancellationToken ct)
        {
            var labelWorkItems = new List<LabelWorkItem>();
            foreach (var label in labels)
            {
                var labelWorkItem = await codeownersManagementHelper.FindLabelByName(label, ct);
                if (labelWorkItem == null)
                {
                    throw new InvalidOperationException($"Label '{label}' does not have a corresponding Label Owner work item in Azure DevOps.");
                }
                labelWorkItems.Add(labelWorkItem);
            }
            return labelWorkItems.ToArray();
        }

        /// <summary>
        /// Determines whether an alias is a team reference (contains a '/' separator, e.g. "azure/my-team").
        /// </summary>
        private static bool IsTeamAlias(string alias)
        {
            return alias.Contains('/');
        }
    }
}
