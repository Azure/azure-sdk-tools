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

        private readonly Option<bool> isMgmtPlaneOption = new("--mgmt-plane")
        {
            Description = "Indicates whether this service is a management-plane library",
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

        private readonly Option<string[]> serviceOwnersOption = new("--service-owners")
        {
            Description = "The service owners (space-separated)",
            Required = false,
            AllowMultipleArgumentsPerToken = true,
        };

        private readonly Option<string[]> sourceOwnersOption = new("--source-owners")
        {
            Description = "The source owners (space-separated)",
            Required = false,
            AllowMultipleArgumentsPerToken = true,
        };

        private readonly Option<bool> isAddingOption = new("--is-adding")
        {
            Description = "Whether to add (true) or remove (false) owners",
            Required = false,
        };

        private readonly Option<string> workingBranchOption = new("--branch")
        {
            Description = "Branch to make edits to, only if provided.",
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

        // Command names
        private const string updateCodeownersCommandName = "update";
        private const string validateCodeownersEntryCommandName = "validate";
        private const string generateCodeownersCommandName = "generate";
        private const string viewCodeownersCommandName = "view";
        private const string exportSectionCommandName = "export-section";
        private const string addCodeownersToPackageCommandName = "add-package-owner";
        private const string addLabelToPackageCommandName = "add-package-label";
        private const string addLabelOwnerCommandName = "add-label-owner";
        private const string removeCodeownersToPackageCommandName = "remove-package-owner";
        private const string removeLabelToPackageCommandName = "remove-package-label";
        private const string removeLabelOwnerCommandName = "remove-label-owner";


        // MCP Tool Names
        private const string CodeownerUpdateToolName = "azsdk_engsys_codeowner_update";
        private const string ValidateCodeownersEntryToolName = "azsdk_engsys_validate_codeowners_entry_for_service";
        private const string CodeownerViewToolName = "azsdk_engsys_codeowner_view";
        private const string CodeownerAddPackageOwnerToolName = "azsdk_engsys_codeowner_add_package_owner";
        private const string CodeownerAddLabelToolName = "azsdk_engsys_codeowner_add_package_label";
        private const string CodeownerAddLabelOwnerToolName = "azsdk_engsys_codeowner_add_label_owner";
        private const string CodeownerRemovePackageOwnerToolName = "azsdk_engsys_codeowner_remove_package_owner";
        private const string CodeownerRemoveLabelToolName = "azsdk_engsys_codeowner_remove_package_label";
        private const string CodeownerRemoveLabelOwnerToolName = "azsdk_engsys_codeowner_remove_label_owner";

        public CodeownersTool(
            IGitHubService githubService,
            ILogger<CodeownersTool> logger,
            ILoggerFactory? loggerFactory,
            ICodeownersValidatorHelper codeownersValidator,
            ICodeownersGenerateHelper codeownersGenerateHelper,
            IGitHelper gitHelper,
            ICodeownersManagementHelper codeownersManagementHelper,
            IDevOpsService devOpsService
        )
        {
            this.githubService = githubService;
            this.logger = logger;
            this.codeownersValidatorHelper = codeownersValidator;
            this.codeownersGenerateHelper = codeownersGenerateHelper;
            this.codeownersManagementHelper = codeownersManagementHelper;
            this.gitHelper = gitHelper;
            this.devOpsService = devOpsService;

            CodeownersUtils.Utils.Log.Configure(loggerFactory);
        }

        protected override List<Command> GetCommands() =>
        [
            new(updateCodeownersCommandName, "Update codeowners in a repository")
            {
                repoOption,
                isMgmtPlaneOption,
                pathOptionOptional,
                serviceLabelOption,
                serviceOwnersOption,
                sourceOwnersOption,
                isAddingOption,
                workingBranchOption,
            },
            new(validateCodeownersEntryCommandName, "Validate codeowners for an existing service entry")
            {
                repoOption, serviceLabelOption, pathOptionOptional,
            },
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
                multipleGithubUserOption, labelsOption, pathOption, ownerTypeOption, optionalRepoOption,
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
                multipleGithubUserOption, labelsOption, pathOption, ownerTypeOption, optionalRepoOption,
            },
            new(exportSectionCommandName, "Export one or more named sections from a CODEOWNERS file")
            {
                codeownersPathOption, sectionsOption, outputFilePathOption,
            }
        ];

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var command = parseResult.CommandResult.Command.Name;

            if (command == updateCodeownersCommandName)
            {
                var repoValue = parseResult.GetValue(repoOption);
                var isMgmtPlaneValue = parseResult.GetValue(isMgmtPlaneOption);
                var pathValue = parseResult.GetValue(pathOptionOptional);
                var serviceLabelValue = parseResult.GetValue(serviceLabelOption);
                var serviceOwnersValue = parseResult.GetValue(serviceOwnersOption);
                var sourceOwnersValue = parseResult.GetValue(sourceOwnersOption);
                var isAddingValue = parseResult.GetValue(isAddingOption);
                var workingBranchValue = parseResult.GetValue(workingBranchOption);

                var addResult = await UpdateCodeowners(
                    repoValue ?? "",
                    isMgmtPlaneValue,
                    pathValue ?? "",
                    serviceLabelValue ?? "",
                    serviceOwnersValue?.ToList() ?? new List<string>(),
                    sourceOwnersValue?.ToList() ?? new List<string>(),
                    isAddingValue,
                    workingBranchValue ?? "");

                return addResult;
            }

            if (command == validateCodeownersEntryCommandName)
            {
                var validateRepo = parseResult.GetValue(repoOption);
                var validateServiceLabel = parseResult.GetValue(serviceLabelOption);
                var validateRepoPath = parseResult.GetValue(pathOptionOptional);

                var validateResult = await ValidateCodeownersEntryForService(
                    validateRepo ?? "",
                    validateServiceLabel,
                    validateRepoPath);

                return validateResult;
            }

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
                return await AddLabelOwner(users!, labels!, ownerType!, path, repo, ct);
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
                return await RemoveLabelOwner(users!, labels!, ownerType!, path, repo, ct);
            }

            if (command == exportSectionCommandName)
            {
                var codeownersPath = parseResult.GetValue(codeownersPathOption);
                var sections = parseResult.GetValue(sectionsOption);
                var output = parseResult.GetValue(outputFilePathOption);
                return await ExportSection(codeownersPath!, sections!, output!);
            }

            return new DefaultCommandResponse { ResponseError = $"Unknown command: '{command}'" };
        }

        [McpServerTool(Name = CodeownerUpdateToolName), Description("Adds or deletes codeowners for a given service label or path in a repo. When isAdding is false, the inputted users will be removed.")]
        public async Task<DefaultCommandResponse> UpdateCodeowners(
            string repo,
            bool isMgmtPlane,
            string path = "",
            string serviceLabel = "",
            List<string> serviceOwners = null,
            List<string> sourceOwners = null,
            bool isAdding = false,
            string workingBranch = "")
        {
            try
            {
                // Validate atleast Service Label or Path.
                if (string.IsNullOrWhiteSpace(serviceLabel) && string.IsNullOrWhiteSpace(path))
                {
                    throw new Exception($"Service label: {serviceLabel} and Path: {path} are both invalid. At least one must be valid");
                }

                if (workingBranch.Equals("main", StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception($"Cannot make changes on branch: {workingBranch}");
                }
                else if (string.IsNullOrEmpty(workingBranch))
                {
                    var codeownersPullRequests = (await githubService.SearchPullRequestsByTitleAsync(Constants.AZURE_OWNER_PATH, repo, "[CODEOWNERS]"))
                        ?? new List<PullRequest?>().AsReadOnly();

                    foreach (var codeownersPullRequest in codeownersPullRequests)
                    {
                        if (codeownersPullRequest != null &&
                            ((!string.IsNullOrEmpty(serviceLabel) && codeownersPullRequest.Title.Contains(serviceLabel, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrEmpty(path) && codeownersPullRequest.Title.Contains(path, StringComparison.OrdinalIgnoreCase))))
                        {
                            workingBranch = codeownersPullRequest.Head.Ref;
                            break;
                        }
                    }
                }

                // Get codeowners file contents.
                var codeownersFileContent = await githubService.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, repo, Constants.AZURE_CODEOWNERS_PATH, workingBranch);

                if (codeownersFileContent == null)
                {
                    throw new Exception("Could not retrieve CODEOWNERS file from the repository.");
                }

                var branchToFetch = string.IsNullOrEmpty(workingBranch) ? "main" : workingBranch;
                var codeownersContent = codeownersFileContent.Content;

                // Use CodeownersEditor for manipulation
                var editor = new CodeownersEditor(codeownersContent, isMgmtPlane);
                CodeownersEntry updatedEntry;
                if (isAdding)
                {
                    updatedEntry = editor.AddOrUpdateCodeownersFile(
                        path: path,
                        serviceLabel: serviceLabel,
                        serviceOwners: serviceOwners,
                        sourceOwners: sourceOwners);
                }
                else
                {
                    updatedEntry = editor.RemoveOwnersFromCodeownersFile(
                        path: path,
                        serviceLabel: serviceLabel,
                        serviceOwnersToRemove: serviceOwners,
                        sourceOwnersToRemove: sourceOwners);
                }

                // Validate the modified/created Entry
                var (validationErrors, codeownersValidationResults) = await ValidateMinimumOwnerRequirements(updatedEntry);

                var codeownersValidationResultMessage = string.Join("\n", codeownersValidationResults.Select(r => r.ToString()));
                if (!string.IsNullOrEmpty(validationErrors))
                {
                    throw new Exception($"{validationErrors} Validation results: {codeownersValidationResultMessage}");
                }

                // Create Branch, Update File, and Handle PR.
                var identifier = !string.IsNullOrWhiteSpace(updatedEntry.ServiceLabels?.FirstOrDefault())
                    ? updatedEntry.ServiceLabels.FirstOrDefault()
                    : updatedEntry.PathExpression;
                var resultMessages = await CreateCodeownersPR(
                    repo,                                                             // Repository name
                    editor.ToString(),                     // Modified content
                    $"Update codeowners entry for {identifier}", // Description for commit message, PR title, and description
                    "update-codeowners-entry",                                             // Branch prefix for the action
                    identifier, // Identifier for the PR
                    workingBranch);

                return new DefaultCommandResponse
                {
                    Result = resultMessages.Concat([codeownersValidationResultMessage])
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while updating codeowners in repository '{RepoName}'.", repo);
                return new DefaultCommandResponse { ResponseError = ex.Message };
            }
        }

        private async Task<List<string>> CreateCodeownersPR(
            string repo,
            string modifiedContent,
            string description, // used for commit message, PR title, and PR description
            string branchPrefix,
            string identifier,
            string workingBranch)
        {
            List<string> resultMessages = new();
            var branchName = "";

            // Check if we have a working branch from SDK generation
            if (!string.IsNullOrEmpty(workingBranch) && await githubService.IsExistingBranchAsync(Constants.AZURE_OWNER_PATH, repo, workingBranch))
            {
                branchName = workingBranch;
                resultMessages.Add($"Using existing branch: {branchName}");
            }
            else
            {
                // Create a new branch only if no working branch exists
                branchName = CreateBranchName(branchPrefix, identifier);
                var createBranchResult = await githubService.CreateBranchAsync(Constants.AZURE_OWNER_PATH, repo, branchName, "main");
                resultMessages.Add($"Created branch: {branchName} - Status: {createBranchResult}");
            }

            // After branchName is set
            var codeownersFileContent = await githubService.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, repo, Constants.AZURE_CODEOWNERS_PATH, branchName);

            if (codeownersFileContent == null)
            {
                throw new Exception("Could not retrieve CODEOWNERS file from the repository.");
            }

            var codeownersSha = codeownersFileContent.Sha;

            // Use codeownersSha in UpdateFileAsync
            await githubService.UpdateFileAsync(Constants.AZURE_OWNER_PATH, repo, Constants.AZURE_CODEOWNERS_PATH, description, modifiedContent, codeownersSha, branchName);

            var prInfoList = await githubService.CreatePullRequestAsync(repo, Constants.AZURE_OWNER_PATH, "main", branchName, "[CODEOWNERS] " + description, description);
            resultMessages.Add($"URL: {prInfoList.Url}");
            resultMessages.AddRange(prInfoList.Messages);
            return resultMessages;
        }

        [McpServerTool(Name = ValidateCodeownersEntryToolName), Description("Validates codeowners in a specific repository for a given service or repo path.")]
        public async Task<ServiceCodeownersResult> ValidateCodeownersEntryForService(string repoName, string? serviceLabel = null, string? path = null)
        {
            ServiceCodeownersResult response = new() { };

            try
            {
                if (string.IsNullOrEmpty(repoName))
                {
                    throw new Exception("Must provide a repository name. Ex. azure-sdk-for-net");
                }

                serviceLabel = serviceLabel?.Trim();
                path = path?.Trim();
                if (string.IsNullOrEmpty(serviceLabel) && string.IsNullOrEmpty(path))
                {
                    throw new Exception("Must provide a service label or a repository path.");
                }

                var workingBranch = "";
                var codeownersPullRequests = await githubService.SearchPullRequestsByTitleAsync(Constants.AZURE_OWNER_PATH, repoName, "[CODEOWNERS]");

                foreach (var codeownersPullRequest in codeownersPullRequests)
                {
                    if (codeownersPullRequest != null &&
                        ((!string.IsNullOrEmpty(serviceLabel) && codeownersPullRequest.Title.Contains(serviceLabel, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(path) && codeownersPullRequest.Title.Contains(path, StringComparison.OrdinalIgnoreCase))))
                    {
                        workingBranch = codeownersPullRequest.Head.Ref;
                    }
                }

                if (workingBranch.Equals("main", StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception($"Cannot make changes on branch: {workingBranch}");
                }

                CodeownersEntry? matchingEntry;
                try
                {
                    var contents = await githubService.GetContentsSingleAsync("Azure", "azure-sdk-for-net", ".github/CODEOWNERS", workingBranch);
                    if (contents == null)
                    {
                        response.Message += "Could not retrieve upstream CODEOWNERS (azure-sdk-for-net) for the requested branch.";
                        return response;
                    }
                    var codeownersContent = contents.Content;
                    var codeownersSha = contents.Sha;
                    var codeownersContentList = codeownersContent.Split("\n").ToList();

                    var codeownersEntries = CodeownersParser.ParseCodeownersEntries(codeownersContentList, azureWriteTeamsBlobUrl);

                    CodeownersEditor codeownersEditor = new CodeownersEditor(codeownersContent);

                    matchingEntry = codeownersEditor.FindMatchingEntry(path, serviceLabel);
                }
                catch (Exception ex)
                {
                    response.Message += $"Error finding service in CODEOWNERS file. Error {ex}";
                    return response;
                }

                // Validate Owners
                if (matchingEntry != null)
                {
                    var validationResponse = await ValidateMinimumOwnerRequirements(matchingEntry);
                    string? validationErrors = validationResponse.validationErrors;
                    List<CodeownersValidationResult>? codeownersValidationResults = validationResponse.codeownersValidationResults;

                    if (!string.IsNullOrEmpty(validationErrors))
                    {
                        response.Message = validationErrors ?? string.Empty;
                    }
                    else
                    {
                        response.Message = "Validation passed: minimum code owner requirements are met.";
                    }
                    response.CodeOwners = codeownersValidationResults ?? new List<CodeownersValidationResult>() { };
                    return response;
                }
                else
                {
                    response.Message += $"Service label '{serviceLabel}' or Repo Path '{path}' not found in {repoName}";
                    return response;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing repository");
                response.Message += $"Error processing repository: {ex.Message}";
                return response;
            }
        }

        private async Task<List<CodeownersValidationResult>> ValidateOwners(IEnumerable<string> owners)
        {
            var validatedOwners = new List<CodeownersValidationResult>();

            foreach (var owner in owners)
            {
                var username = owner.TrimStart('@');
                var result = await codeownersValidatorHelper.ValidateCodeOwnerAsync(username, verbose: false);

                if (string.IsNullOrEmpty(result.Username))
                {
                    result.Username = username;
                }

                validatedOwners.Add(result);
            }

            return validatedOwners;
        }

        private async Task<(string validationErrors, List<CodeownersValidationResult> codeownersValidationResults)> ValidateMinimumOwnerRequirements(CodeownersEntry codeownersEntry)
        {
            var validatedServiceOwners = await ValidateOwners(codeownersEntry.ServiceOwners);
            var validatedSourceOwners = await ValidateOwners(codeownersEntry.SourceOwners);
            var validatedAzureSdkOwners = await ValidateOwners(codeownersEntry.AzureSdkOwners);

            var validServiceOwnersCount = validatedServiceOwners.Count(owner => owner.IsValidCodeOwner);
            var validSourceOwnersCount = validatedSourceOwners.Count(owner => owner.IsValidCodeOwner);

            var validationErrors = new List<string>();

            if (!string.IsNullOrEmpty(codeownersEntry.ServiceLabels.FirstOrDefault()) && validServiceOwnersCount < 2)
            {
                validationErrors.Add("There must be at least two valid service owners.");
            }
            if (!string.IsNullOrEmpty(codeownersEntry.PathExpression) && validSourceOwnersCount < 2)
            {
                validationErrors.Add("There must be at least two valid source owners.");
            }

            var allValidationResults = new List<CodeownersValidationResult>();
            allValidationResults.AddRange(validatedServiceOwners);
            allValidationResults.AddRange(validatedSourceOwners);
            allValidationResults.AddRange(validatedAzureSdkOwners);

            // Remove duplicates by Username (case-insensitive)
            var distinctResults = allValidationResults
            .GroupBy(r => r.Username?.ToLowerInvariant())
            .Select(g => g.First())
            .ToList();

            if (validationErrors.Any())
            {
                return (string.Join(" ", validationErrors), distinctResults);
            }
            return ("", distinctResults);
        }

        private string CreateBranchName(string prefix, string identifier)
        {
            var normalizedIdentifier = identifier
                .Replace(" - ", "-")
                .Replace(" ", "-")
                .Replace("/", "-")
                .Replace("_", "-")
                .Replace(".", "-")
                .Trim('-')
                .ToLowerInvariant();

            normalizedIdentifier = Regex.Replace(normalizedIdentifier, @"[^a-zA-Z0-9\-]", "");

            return $"{prefix}-{normalizedIdentifier}";
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

                var repo = await gitHelper.GetRepoFullNameAsync(repoRoot);

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
        ) {
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
                    return await codeownersManagementHelper.GetViewByUser(githubUser, repo);
                }
                if (hasLabels)
                {
                    return await codeownersManagementHelper.GetViewByLabel(labels, repo);
                }
                if (!string.IsNullOrEmpty(package))
                {
                    return await codeownersManagementHelper.GetViewByPackage(package, repo);
                }
                return await codeownersManagementHelper.GetViewByPath(path!, repo);
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
            string output)
        {
            if (!File.Exists(codeownersPath))
            {
                return new DefaultCommandResponse
                {
                    ResponseError = $"CODEOWNERS file not found: {codeownersPath}"
                };
            }

            var lines = (await File.ReadAllLinesAsync(codeownersPath)).ToList();
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

            await File.WriteAllLinesAsync(output, exportedLines);

            return new DefaultCommandResponse
            {
                Message = $"Exported {sections.Length} section(s) to {output}"
            };
        }

        [McpServerTool(Name = CodeownerAddPackageOwnerToolName), Description("Add source owner(s) to a package in CODEOWNERS work items.")]
        public async Task<CommandResponse> AddPackageOwner(
            string[] githubUsers,
            string package,
            string? repo = null,
            CancellationToken ct = default
        ) {
            try
            {
                repo = await ResolveRepo(repo, ct);
                return await codeownersManagementHelper.AddOwnersToPackage(
                    await FindOrCreateOwnerWorkItems(githubUsers),
                    package,
                    repo);
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
        ) {
            try
            {
                repo = await ResolveRepo(repo, ct);
                return await codeownersManagementHelper.AddLabelsToPackage(
                    await FindLabels(labels),
                    package,
                    repo
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
            CancellationToken ct = default
        ) {
            try
            {
                repo = await ResolveRepo(repo, ct);
                return await codeownersManagementHelper.AddOwnersAndLabelsToPath(
                    await FindOrCreateOwnerWorkItems(githubUsers),
                    await FindLabels(labels),
                    repo,
                    path,
                    ownerType
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
        ) {
            try
            {
                repo = await ResolveRepo(repo, ct);
                return await codeownersManagementHelper.RemoveOwnersFromPackage(
                    await GetOwnerWorkItems(githubUsers),
                    package,
                    repo
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
        ) {
            try
            {
                repo = await ResolveRepo(repo, ct);
                return await codeownersManagementHelper.RemoveLabelsFromPackage(
                    await FindLabels(labels),
                    package,
                    repo
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
            CancellationToken ct = default
        ) {
            try
            {
                repo = await ResolveRepo(repo, ct);
                return await codeownersManagementHelper.RemoveOwnersFromLabelsAndPath(
                    await GetOwnerWorkItems(githubUsers),
                    await FindLabels(labels),
                    repo,
                    path,
                    ownerType
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error removing label owner(s)");
                return new DefaultCommandResponse { ResponseError = ex.Message };
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

        private async Task<OwnerWorkItem[]> GetOwnerWorkItems(string[] ownerAliases)
        {
            var ownerWorkItems = new List<OwnerWorkItem>();
            foreach (var alias in ownerAliases)
            {
                var ownerWorkItem = await codeownersManagementHelper.FindOwnerByGitHubAlias(alias);
                if (ownerWorkItem == null)
                {
                    throw new Exception($"GitHub alias '{alias}' does not have a corresponding Owner work item in Azure DevOps.");
                }
                ownerWorkItems.Add(ownerWorkItem);
            }
            return ownerWorkItems.ToArray();
        }

        private async Task<OwnerWorkItem[]> FindOrCreateOwnerWorkItems(string[] ownerAliases)
        {
            var ownerWorkItems = new List<OwnerWorkItem>();
            foreach(var alias in ownerAliases)
            {
                var existing = await codeownersManagementHelper.FindOwnerByGitHubAlias(alias);
                if (existing != null)
                {
                    ownerWorkItems.Add(existing);
                    continue;
                }

                var validation = await codeownersValidatorHelper.ValidateCodeOwnerAsync(alias, verbose: false);
                if (!validation.IsValidCodeOwner)
                {
                    throw new InvalidOperationException(
                        $"GitHub user '{alias}' is not a valid Azure SDK code owner: {validation.Message}");
                }

                var ownerWi = new OwnerWorkItem { GitHubAlias = alias };
                var created = await devOpsService.CreateWorkItemAsync(ownerWi, "Owner", alias);
                ownerWorkItems.Add(WorkItemMappers.MapToOwnerWorkItem(created));
            }
            return ownerWorkItems.ToArray();
        }

        private async Task<LabelWorkItem[]> FindLabels(string[] labels)
        {
            var labelWorkItems = new List<LabelWorkItem>();
            foreach (var label in labels)
            {
                var labelWorkItem = await codeownersManagementHelper.FindLabelByName(label);
                if (labelWorkItem == null)
                {
                    throw new InvalidOperationException($"Label '{label}' does not have a corresponding Label Owner work item in Azure DevOps.");
                }
                labelWorkItems.Add(labelWorkItem);
            }
            return labelWorkItems.ToArray();
        }
    }
}
