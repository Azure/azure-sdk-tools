using System.ComponentModel;
using System.CommandLine;
using System.Text.RegularExpressions;

using ModelContextProtocol.Server;
using Octokit;

using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.CodeownersUtils.Editing;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Models.Responses;
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
        private readonly Option<string> viewUserOption = new("--user")
        {
            Description = "GitHub alias to look up",
            Required = false,
        };

        private readonly Option<string> viewLabelOption = new("--label")
        {
            Description = "Label name to look up (for view) or label(s) for add/remove",
            Required = false,
        };

        private readonly Option<string[]> labelsOption = new("--label")
        {
            Description = "Label name(s). Can be specified multiple times.",
            Required = false,
            AllowMultipleArgumentsPerToken = true,
        };

        private readonly Option<string> viewPackageOption = new("--package")
        {
            Description = "Package name to look up",
            Required = false,
        };

        private readonly Option<string> mgmtPathOption = new("--path", "-p")
        {
            Description = "Repository path (e.g., sdk/formrecognizer/)",
            Required = false,
        };

        private readonly Option<string> mgmtRepoOption = new("--repo", "-r")
        {
            Description = "Repository name (e.g., Azure/azure-sdk-for-python)",
            Required = false,
        };

        private readonly Option<string> mgmtRepoRequiredOption = new("--repo", "-r")
        {
            Description = "Repository name (e.g., Azure/azure-sdk-for-python)",
            Required = true,
        };

        private readonly Option<string> ownerTypeOption = new("--owner-type")
        {
            Description = "Owner type: service-owner, azsdk-owner, or pr-label",
            Required = false,
        };

        private readonly Option<string> mgmtUserOption = new("--user")
        {
            Description = "GitHub alias of the owner",
            Required = false,
        };

        private readonly Option<string> mgmtPackageOption = new("--package")
        {
            Description = "Package name",
            Required = false,
        };

        private readonly IGitHubService githubService;
        private readonly ILogger<CodeownersTool> logger;
        private readonly ICodeownersValidatorHelper codeownersValidatorHelper;
        private readonly ICodeownersGenerateHelper codeownersGenerateHelper;
        private readonly ICodeownersManagementHelper codeownersManagementHelper;
        private readonly IGitHelper gitHelper;

        // URL constants
        private const string azureWriteTeamsBlobUrl = "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/azure-sdk-write-teams-blob";

        // Command names
        private const string updateCodeownersCommandName = "update";
        private const string validateCodeownersEntryCommandName = "validate";
        private const string generateCodeownersCommandName = "generate";
        private const string viewCodeownersCommandName = "view";
        private const string addCodeownersCommandName = "add";
        private const string removeCodeownersCommandName = "remove";

        // MCP Tool Names
        private const string CodeownerUpdateToolName = "azsdk_engsys_codeowner_update";
        private const string ValidateCodeownersEntryToolName = "azsdk_engsys_validate_codeowners_entry_for_service";
        private const string CodeownerViewToolName = "azsdk_engsys_codeowner_view";
        private const string CodeownerAddToolName = "azsdk_engsys_codeowner_add";
        private const string CodeownerRemoveToolName = "azsdk_engsys_codeowner_remove";

        public CodeownersTool(
            IGitHubService githubService,
            ILogger<CodeownersTool> logger,
            ILoggerFactory? loggerFactory,
            ICodeownersValidatorHelper codeownersValidator,
            ICodeownersGenerateHelper codeownersGenerateHelper,
            IGitHelper gitHelper,
            ICodeownersManagementHelper codeownersManagementHelper
        )
        {
            this.githubService = githubService;
            this.logger = logger;
            this.codeownersValidatorHelper = codeownersValidator;
            this.codeownersGenerateHelper = codeownersGenerateHelper;
            this.codeownersManagementHelper = codeownersManagementHelper;
            this.gitHelper = gitHelper;

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
                viewUserOption, viewLabelOption, viewPackageOption, mgmtPathOption, mgmtRepoOption,
            },
            new(addCodeownersCommandName, "Add ownership relationships between DevOps work items")
            {
                mgmtRepoRequiredOption, mgmtUserOption, mgmtPackageOption, labelsOption, mgmtPathOption, ownerTypeOption,
            },
            new(removeCodeownersCommandName, "Remove ownership relationships between DevOps work items")
            {
                mgmtRepoRequiredOption, mgmtUserOption, mgmtPackageOption, labelsOption, mgmtPathOption, ownerTypeOption,
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
                    parseResult.GetValue(repoRootOption)
                );
                var packageTypes = parseResult.GetValue(packageTypesOption);
                var section = parseResult.GetValue(sectionOption);
                var generateResult = await GenerateCodeowners(repoRoot, packageTypes, section, ct);
                return generateResult;
            }

            if (command == viewCodeownersCommandName)
            {
                var user = parseResult.GetValue(viewUserOption);
                var label = parseResult.GetValue(viewLabelOption);
                var package = parseResult.GetValue(viewPackageOption);
                var path = parseResult.GetValue(mgmtPathOption);
                var repo = parseResult.GetValue(mgmtRepoOption);
                return await ViewCodeowners(user, label, package, path, repo);
            }

            if (command == addCodeownersCommandName)
            {
                var repo = parseResult.GetValue(mgmtRepoRequiredOption) ?? "";
                var user = parseResult.GetValue(mgmtUserOption);
                var package = parseResult.GetValue(mgmtPackageOption);
                var labels = parseResult.GetValue(labelsOption)?.ToList() ?? [];
                var path = parseResult.GetValue(mgmtPathOption);
                var ownerType = parseResult.GetValue(ownerTypeOption);
                return await AddCodeowners(repo, user, package, labels, path, ownerType);
            }

            if (command == removeCodeownersCommandName)
            {
                var repo = parseResult.GetValue(mgmtRepoRequiredOption) ?? "";
                var user = parseResult.GetValue(mgmtUserOption);
                var package = parseResult.GetValue(mgmtPackageOption);
                var labels = parseResult.GetValue(labelsOption)?.ToList() ?? [];
                var path = parseResult.GetValue(mgmtPathOption);
                var ownerType = parseResult.GetValue(ownerTypeOption);
                return await RemoveCodeowners(repo, user, package, labels, path, ownerType);
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

        [McpServerTool(Name = CodeownerViewToolName), Description("View CODEOWNERS associations for a user, label, package, or path. Exactly one of --user, --label, --package, or --path must be specified.")]
        public async Task<CommandResponse> ViewCodeowners(
            string? user = null,
            string? label = null,
            string? package = null,
            string? path = null,
            string? repo = null)
        {
            try
            {
                // Validate exactly one lookup axis
                var axes = new[] { user, label, package, path }.Count(a => !string.IsNullOrEmpty(a));
                if (axes == 0)
                {
                    return new DefaultCommandResponse { ResponseError = "Exactly one of --user, --label, --package, or --path must be specified." };
                }
                if (axes > 1)
                {
                    return new DefaultCommandResponse { ResponseError = "Only one of --user, --label, --package, or --path can be specified at a time." };
                }

                if (!string.IsNullOrEmpty(user))
                {
                    return await codeownersManagementHelper.GetViewByUserAsync(user, repo);
                }
                if (!string.IsNullOrEmpty(label))
                {
                    return await codeownersManagementHelper.GetViewByLabelAsync(label, repo);
                }
                if (!string.IsNullOrEmpty(package))
                {
                    return await codeownersManagementHelper.GetViewByPackageAsync(package);
                }
                return await codeownersManagementHelper.GetViewByPathAsync(path!, repo);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error viewing codeowners data");
                return new DefaultCommandResponse { ResponseError = ex.Message };
            }
        }

        [McpServerTool(Name = CodeownerAddToolName), Description("Add ownership relationships between DevOps work items. Requires --repo. Use --user+--package, --user+--label+--owner-type, --user+--path+--owner-type, or --label+--path.")]
        public async Task<CommandResponse> AddCodeowners(
            string repo,
            string? user = null,
            string? package = null,
            List<string>? label = null,
            string? path = null,
            string? ownerType = null)
        {
            try
            {
                var labels = label ?? [];
                var validationError = ValidateAddRemoveParams(user, package, labels, path, ownerType, isAdd: true);
                if (validationError != null)
                {
                    return new DefaultCommandResponse { ResponseError = validationError };
                }

                string result;
                if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(package))
                {
                    result = await codeownersManagementHelper.AddOwnerToPackageAsync(user, package, repo);
                }
                else if (!string.IsNullOrEmpty(user) && labels.Count > 0)
                {
                    result = await codeownersManagementHelper.AddOwnerToLabelAsync(user, labels, repo, ownerType!, path);
                }
                else if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(path))
                {
                    result = await codeownersManagementHelper.AddOwnerToPathAsync(user, repo, path, ownerType!);
                }
                else
                {
                    result = await codeownersManagementHelper.AddLabelToPathAsync(labels, repo, path!);
                }

                return new DefaultCommandResponse
                {
                    Message = result,
                    NextSteps = ["Run the 'generate' command to regenerate the CODEOWNERS file from the updated work items."]
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error adding codeowners relationship");
                return new DefaultCommandResponse { ResponseError = ex.Message };
            }
        }

        [McpServerTool(Name = CodeownerRemoveToolName), Description("Remove ownership relationships between DevOps work items. Requires --repo. Same parameter rules as add.")]
        public async Task<CommandResponse> RemoveCodeowners(
            string repo,
            string? user = null,
            string? package = null,
            List<string>? label = null,
            string? path = null,
            string? ownerType = null)
        {
            try
            {
                var labels = label ?? [];
                var validationError = ValidateAddRemoveParams(user, package, labels, path, ownerType, isAdd: false);
                if (validationError != null)
                {
                    return new DefaultCommandResponse { ResponseError = validationError };
                }

                string result;
                if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(package))
                {
                    result = await codeownersManagementHelper.RemoveOwnerFromPackageAsync(user, package, repo);
                }
                else if (!string.IsNullOrEmpty(user) && labels.Count > 0)
                {
                    result = await codeownersManagementHelper.RemoveOwnerFromLabelAsync(user, labels, repo, ownerType!);
                }
                else if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(path))
                {
                    result = await codeownersManagementHelper.RemoveOwnerFromPathAsync(user, repo, path, ownerType!);
                }
                else
                {
                    result = await codeownersManagementHelper.RemoveLabelFromPathAsync(labels, repo, path!);
                }

                return new DefaultCommandResponse
                {
                    Message = result,
                    NextSteps = ["Run the 'generate' command to regenerate the CODEOWNERS file from the updated work items."]
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error removing codeowners relationship");
                return new DefaultCommandResponse { ResponseError = ex.Message };
            }
        }

        /// <summary>
        /// Validates parameter combinations for add/remove commands.
        /// </summary>
        internal static string? ValidateAddRemoveParams(string? user, string? package, List<string> labels, string? path, string? ownerType, bool isAdd)
        {
            var hasUser = !string.IsNullOrEmpty(user);
            var hasPackage = !string.IsNullOrEmpty(package);
            var hasLabels = labels.Count > 0;
            var hasPath = !string.IsNullOrEmpty(path);
            var hasOwnerType = !string.IsNullOrEmpty(ownerType);

            // Scenario: User + Package
            if (hasUser && hasPackage)
            {
                if (hasOwnerType)
                {
                    return "Cannot specify --owner-type when adding/removing a user from a package. Packages only have source owners.";
                }
                return null;
            }

            // Scenario: User + Label(s)
            if (hasUser && hasLabels)
            {
                if (!hasOwnerType)
                {
                    return "Must specify --owner-type (service-owner, azsdk-owner, or pr-label) when adding/removing a user from a label.";
                }
                if (ownerType!.Equals("pr-label", StringComparison.OrdinalIgnoreCase) && !hasPath)
                {
                    return "Must specify --path when using --owner-type pr-label.";
                }
                return null;
            }

            // Scenario: User + Path (no labels, no package)
            if (hasUser && hasPath && !hasPackage && !hasLabels)
            {
                if (!hasOwnerType)
                {
                    return "Must specify --owner-type (service-owner, azsdk-owner, or pr-label) when adding/removing a user from a path.";
                }
                return null;
            }

            // Scenario: Label(s) + Path (no user)
            if (hasLabels && hasPath && !hasUser)
            {
                if (hasOwnerType)
                {
                    return "Cannot specify --owner-type when adding/removing labels to/from a path.";
                }
                return null;
            }

            return "Invalid parameter combination. Use: --user+--package, --user+--label+--owner-type, --user+--path+--owner-type, or --label+--path.";
        }
    }
}
