using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Tools;
using ModelContextProtocol.Server;
using System.CommandLine;
using Azure.Sdk.Tools.Cli.Contract;
using System.CommandLine.Invocation;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Octokit;
using Azure.Sdk.Tools.CodeownersUtils.Utils;
using Microsoft.TeamFoundation.Common;
using Azure.Sdk.Tools.Cli.Configuration;

namespace Azure.Sdk.Tools.Cli.Tools
{
    [Description("Tool that validates and manipulates codeowners files.")]
    [McpServerToolType]
    public class CodeownerTools(
        IGitHubService githubService,
        IOutputService output,
        ILogger<CodeownerTools> logger,
        ITypeSpecHelper typespecHelper,
        ICodeOwnerHelper codeownerHelper,
        ICodeOwnerValidatorHelper codeOwnerValidator,
        ILabelHelper labelHelper) : MCPTool
    {
        private static Dictionary<string, CodeOwnerValidationResult> codeOwnerValidationCache = new Dictionary<string, CodeOwnerValidationResult>();
        private static readonly string standardServiceCategory = "# Client Libraries";
        private static readonly string standardManagementCategory = "# Management Libraries";

        // URL constants
        private const string azureWriteTeamsBlobUrl = "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/azure-sdk-write-teams-blob";
        private const string githubRawContentBaseUrl = "https://raw.githubusercontent.com";

        // Command names
        private const string updateCodeownersCommandName = "update-codeowners";
        private const string validateCodeOwnerEntryCommandName = "validate-codeowner-entry";

        // Core command options
        private readonly Option<string> repoOption = new(["--repo", "-r"], "The repository name") { IsRequired = true };
        private readonly Option<string> pathOption = new(["--path", "-p"], "The path for the codeowners entry") { IsRequired = true };
        private readonly Option<string> pathOptionOptional = new(["--path", "-p"], "The repository path to check/validate");
        private readonly Option<string> serviceLabelOption = new(["--service-label", "-sl"], "The service label");
        private readonly Option<string[]> serviceOwnersOption = new(["--service-owners", "-so"], "The service owners (space-separated)");
        private readonly Option<string[]> sourceOwnersOption = new(["--source-owners", "-sro"], "The source owners (space-separated)") { IsRequired = true };
        private readonly Option<string> typeSpecProjectPathOption = new(["--typespec-project"], "Path to typespec project") { IsRequired = true };
        private readonly Option<bool> isAddingOption = new(["--is-adding", "-ia"], "Whether to add (true) or remove (false) owners") { IsRequired = true };
        private readonly Option<string> workingBranchOption = new(["--working-branch", "-wb"], "The existing branch to add changes to (from SDK generation)");

        public override Command GetCommand()
        {
            var command = new Command("codeowner-tools", "A tool to validate and modify codeowners.");
            var subCommands = new[]
            {
                new Command(updateCodeownersCommandName, "Update codeowners in a repository")
                {
                    repoOption,
                    typeSpecProjectPathOption,
                    pathOption,
                    serviceLabelOption,
                    serviceOwnersOption,
                    sourceOwnersOption,
                    isAddingOption,
                    workingBranchOption
                },
                new Command(validateCodeOwnerEntryCommandName, "Validate code owners for an existing service entry")
                {
                    repoOption,
                    serviceLabelOption,
                    pathOptionOptional
                },
            };

            foreach (var subCommand in subCommands)
            {
                subCommand.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
                command.AddCommand(subCommand);
            }
            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            var command = ctx.ParseResult.CommandResult.Command.Name;
            var commandParser = ctx.ParseResult;

            switch (command)
            {
                case updateCodeownersCommandName:
                    var repoValue = commandParser.GetValueForOption(repoOption);
                    var typespecPathValue = commandParser.GetValueForOption(typeSpecProjectPathOption);
                    var pathValue = commandParser.GetValueForOption(pathOption);
                    var serviceLabelValue = commandParser.GetValueForOption(serviceLabelOption);
                    var serviceOwnersValue = commandParser.GetValueForOption(serviceOwnersOption);
                    var sourceOwnersValue = commandParser.GetValueForOption(sourceOwnersOption);
                    var isAddingValue = commandParser.GetValueForOption(isAddingOption);
                    var workingBranchValue = commandParser.GetValueForOption(workingBranchOption);

                    var addResult = await UpdateCodeowners(
                        repoValue ?? "",
                        typespecPathValue ?? "",
                        pathValue ?? "",
                        serviceLabelValue ?? "",
                        serviceOwnersValue?.ToList() ?? new List<string>(),
                        sourceOwnersValue.ToList() ?? new List<string>(),
                        isAddingValue,
                        workingBranchValue ?? "");
                    output.Output(addResult);
                    return;
                case validateCodeOwnerEntryCommandName:
                    var validateRepo = commandParser.GetValueForOption(repoOption);
                    var validateServiceLabel = commandParser.GetValueForOption(serviceLabelOption);
                    var validateRepoPath = commandParser.GetValueForOption(pathOptionOptional);

                    var validateResult = await ValidateCodeOwnerEntryForService(
                        validateRepo ?? "",
                        validateServiceLabel,
                        validateRepoPath);
                    output.Output(validateResult);
                    return;
                default:
                    SetFailure();
                    output.OutputError($"Unknown command: '{command}'");
                    return;
            }
        }

        [McpServerTool(Name = "UpdateCodeowners"), Description("Adds or deletes codeowners for a given service label or path in a repo.")]
        public async Task<string> UpdateCodeowners(
            string repo,
            string typeSpecProjectRoot,
            string path = null,
            string serviceLabel = null,
            List<string> serviceOwners = null,
            List<string> sourceOwners = null,
            bool isAdding = false,
            string workingBranch = null)
        {
            try
            {
                // Validate atleast Service Label or Path.
                if (string.IsNullOrEmpty(serviceLabel) && string.IsNullOrEmpty(path))
                {
                    throw new Exception($"Service label: {serviceLabel} and Path: {path} are both invalid. Atleast one must be valid");
                }
                // Check if it's management plane.
                var isMgmtPlane = typespecHelper.IsTypeSpecProjectForMgmtPlane(typeSpecProjectRoot);

// Get file contents. THIS WILL LIKELY BE CHANGED AS LABELS PR HAS MODIFICATIONS TO HOW THIS METHOD IS CALLED AND USED
                var codeownersFileContent = await githubService.GetContentsAsync(Constants.AZURE_OWNER_PATH, repo, Constants.AZURE_CODEOWNERS_PATH);
                if (codeownersFileContent == null || codeownersFileContent.Count == 0)
                {
                    throw new Exception($"Could not retrieve CODEOWNERS file with repository path '{Constants.AZURE_OWNER_PATH}/{repo}/{Constants.AZURE_CODEOWNERS_PATH}'");
                }
                var codeownersUrl = $"{githubRawContentBaseUrl}/{Constants.AZURE_OWNER_PATH}/{repo}/main/{Constants.AZURE_CODEOWNERS_PATH}";
                var codeownersContent = codeownersFileContent.FirstOrDefault()?.Content;
                var codeownersSha = codeownersFileContent.FirstOrDefault()?.Sha;

                var labelsFileContent = await githubService.GetContentsAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, Constants.AZURE_COMMON_LABELS_PATH);
                if (labelsFileContent == null || labelsFileContent.Count == 0)
                {
                    throw new Exception($"Could not retrieve labels file with repository path '{Constants.AZURE_OWNER_PATH}/{Constants.AZURE_SDK_TOOLS_PATH}/{Constants.AZURE_COMMON_LABELS_PATH}'");
                }
                var labelsContent = labelsFileContent.FirstOrDefault()?.Content;
                var labelsSha = labelsFileContent.FirstOrDefault()?.Sha;

                // Validate service path
                if (!string.IsNullOrEmpty(path))
                {
                    path = path.Trim('/');
                    path = $"/{path}/";
                }

                // Validate service label
                if (!string.IsNullOrEmpty(serviceLabel))
                {
                    var serviceLabelValidationResults = labelHelper.CheckServiceLabel(labelsContent, serviceLabel);
                    if (serviceLabelValidationResults != LabelHelper.ServiceLabelStatus.Exists)
                    {
                        var pullRequests = await githubService.SearchPullRequestsByTitleAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, "Service Label");

                        if (!labelHelper.CheckServiceLabelInReview(pullRequests, serviceLabel))
                        {
                            throw new Exception($"Service label: {serviceLabel} is invalid.");
                        }
                    }
                }

                // Find Codeowner Entry with the validated Label or Path
                var (startLine, endLine) = (-1, -1);
                if (isMgmtPlane)
                {
                    (startLine, endLine) = codeownerHelper.findBlock(codeownersContent, standardManagementCategory);
                }
                else
                {
                    (startLine, endLine) = codeownerHelper.findBlock(codeownersContent, standardServiceCategory);
                }

                var codeownersEntries = CodeownersParser.ParseCodeownersFile(codeownersUrl, azureWriteTeamsBlobUrl, startLine, endLine);
                for (int i = 0; i < codeownersEntries.Count; i++)
                {
                    (codeownersEntries, i) = codeownerHelper.mergeCodeownerEntries(codeownersEntries, i);
                }

                CodeownersEntry? updatedEntry = null;
                if (!string.IsNullOrEmpty(path))
                {
                    // get the first entry that matches if no one get null. Then compare the path of the entry with the given path 
                    updatedEntry = codeownersEntries.FirstOrDefault(entry =>
                        entry.PathExpression?.Equals(path, StringComparison.OrdinalIgnoreCase) == true);
                }
                else if (!string.IsNullOrEmpty(serviceLabel))
                {   // search for service label in the service labels of the entries
                    updatedEntry = codeownersEntries.FirstOrDefault(entry =>
                        entry.ServiceLabels?.Any(label => label.Equals(serviceLabel, StringComparison.OrdinalIgnoreCase)) == true);
                }

                var codeownersEntryExists = false;
                // If the Entry exists
                if (updatedEntry != null)
                {
                    codeownersEntryExists = true;
                    // Update target entry with new codeowners
                    if (isAdding)
                    {
                        updatedEntry.ServiceOwners = codeownerHelper.AddUniqueOwners(updatedEntry.ServiceOwners, serviceOwners);
                        updatedEntry.SourceOwners = codeownerHelper.AddUniqueOwners(updatedEntry.SourceOwners, sourceOwners);
                    }
                    else
                    {
                        updatedEntry.ServiceOwners = codeownerHelper.RemoveOwners(updatedEntry.ServiceOwners, serviceOwners);
                        updatedEntry.SourceOwners = codeownerHelper.RemoveOwners(updatedEntry.SourceOwners, sourceOwners);
                    }
                }

                // Else (Entry doesn't exist)
                if (updatedEntry == null)
                {
                    // Only check if path already exists when adding new entry.
                    if (codeownersContent?.Contains(path) == true)
                    {
                        throw new Exception($"{path} already exists in the CODEOWNERS file.");
                    }

                    codeownersEntryExists = false;
                    updatedEntry = new CodeownersEntry()
                    {
                        PathExpression = path ?? string.Empty,
                        ServiceLabels = new List<string>() { serviceLabel } ?? new List<string>(),
                        ServiceOwners = serviceOwners ?? new List<string>(),
                        SourceOwners = sourceOwners ?? new List<string>(),
                        AzureSdkOwners = new List<string>()
                    };
                }

                // Validate the modified/created Entry
                await ValidateMinimumOwnerRequirements(updatedEntry.ServiceOwners, updatedEntry.SourceOwners, updatedEntry.ServiceLabels.FirstOrDefault(), updatedEntry.PathExpression);

                // Modify the file
                var insertionIndex = codeownerHelper.findAlphabeticalInsertionPoint(codeownersEntries, path, serviceLabel);
                var modifiedCodeownersContent = codeownerHelper.addCodeownersEntryAtIndex(codeownersContent, updatedEntry, insertionIndex, codeownersEntryExists);

                // Create Branch, Update File, and Handle PR.
                var actionDescription = isAdding ? "Add codeowner aliases for" : "Remove codeowner aliases for";
                var actionType = isAdding ? "add-codeowner-alias" : "remove-codeowner-alias";
                
                var resultMessages = await CreateCodeownerPR(
                    repo,                                              // Repository name
                    string.Join('\n', modifiedCodeownersContent),                     // Modified content
                    codeownersSha,                                                    // SHA of the file to update 
                    $"{actionDescription} {updatedEntry.ServiceLabels?.FirstOrDefault() ?? updatedEntry.PathExpression}", // Description for commit message, PR title, and description
                    actionType,                                             // Branch prefix for the action
                    updatedEntry.ServiceLabels?.FirstOrDefault() ?? updatedEntry.PathExpression, // Identifier for the PR
                    workingBranch);

                return string.Join("\n", resultMessages);
            }
            catch (Exception ex)
            {
                return $"Error: {ex}";
            }
        }

        [McpServerTool(Name = "ValidateCodeOwnerEntryForService"), Description("Validates code owners in a specific repository for a given service or repo path.")]
        public async Task<ServiceCodeOwnerResult> ValidateCodeOwnerEntryForService(string repoName, string? serviceLabel = null, string? repoPath = null)
        {
            ServiceCodeOwnerResult response = new() { };

            try
            {
                if (string.IsNullOrEmpty(serviceLabel) && string.IsNullOrEmpty(repoPath))
                {
                    response.Message += "Must provide a service label or a repository path.";
                    return response;
                }

                List<CodeownersEntry?>? matchingEntries;

                // Find Codeowners Entries
                try
                {
                    var codeownersUrl = $"{githubRawContentBaseUrl}/{Constants.AZURE_OWNER_PATH}/{repoName}/main/{Constants.AZURE_CODEOWNERS_PATH}";
                    var codeownersEntries = CodeownersParser.ParseCodeownersFile(codeownersUrl, azureWriteTeamsBlobUrl);
                    matchingEntries = codeownerHelper.FindMatchingEntries(codeownersEntries, serviceLabel, repoPath);
                }
                catch (Exception ex)
                {
                    var errorMessage = $"Error finding service in CODEOWNERS file. Error {ex}";
                    logger.LogError(errorMessage);
                    response.Message += errorMessage;
                    return response;
                }

                // Validate Owners
                if (matchingEntries != null && matchingEntries.Count > 0)
                {
                    var uniqueOwners = new HashSet<string>();
                    foreach (var matchingEntry in matchingEntries)
                    {
                        var owners = codeownerHelper.ExtractUniqueOwners(matchingEntry);
                        foreach (var owner in owners)
                        {
                            uniqueOwners.Add(owner);
                        }
                    }

                    var codeOwners = await ValidateOwners(uniqueOwners.Select(owner => owner.TrimStart('@')));
                    response.CodeOwners = codeOwners;
                    response.Message += "Successfully found and validated codeowners.";
                    response.Repository = repoName ?? string.Empty;
                    return response;
                }
                else
                {
                    response.Message += $"Service label '{serviceLabel}' or Repo Path '{repoPath}' not found in {repoName}";
                    return response;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing repository {repo}", repoName);
                response.Message += $"Error processing repository: {ex.Message}";
                return response;
            }
        }

        private async Task<List<string>> CreateCodeownerPR(
            string repo,
            string modifiedContent,
            string sha,
            string description, // used for commit message, PR title, and PR description
            string branchPrefix,
            string identifier,
            string workingBranch = null)
        {
            List<string> resultMessages = new();
            var branchName = "";

            // Check if we have a working branch from SDK generation
            if (!string.IsNullOrEmpty(workingBranch) && await githubService.GetBranchAsync(Constants.AZURE_OWNER_PATH, repo, workingBranch))
            {
                branchName = workingBranch;
                resultMessages.Add($"Using existing branch: {branchName}");
            }
            else
            {
                // Create a new branch only if no working branch exists
                branchName = codeownerHelper.CreateBranchName(branchPrefix, identifier);
                var createBranchResult = await githubService.CreateBranchAsync(Constants.AZURE_OWNER_PATH, repo, branchName);
                resultMessages.Add($"Created branch: {branchName} - Status: {createBranchResult}");
            }

            // Update file
            await githubService.UpdateFileAsync(Constants.AZURE_OWNER_PATH, repo, Constants.AZURE_CODEOWNERS_PATH, description, modifiedContent, sha, branchName);

            // Handle PR creation or update existing PR
            var existingPR = await githubService.GetPullRequestForBranchAsync(Constants.AZURE_OWNER_PATH, repo, branchName);
            if (existingPR != null)
            {
                // PR already exists - the file update will automatically be added to it
                resultMessages.Add($"Codeowner changes added to existing PR #{existingPR.Number}: {existingPR.HtmlUrl}");
            }
            else
            {
                // No existing PR - create a new one if needed
                var prInfoList = await githubService.CreatePullRequestAsync(repo, Constants.AZURE_OWNER_PATH, "main", branchName, description, description, true);
                resultMessages.AddRange(prInfoList);
            }

            return resultMessages;
        }

        private async Task<List<CodeOwnerValidationResult>> ValidateOwners(IEnumerable<string> owners)
        {
            var validatedOwners = new List<CodeOwnerValidationResult>();
            
            foreach (var owner in owners)
            {
                var username = owner.TrimStart('@');
                if (codeOwnerValidationCache.TryGetValue(username, out var cachedResult))
                {
                    validatedOwners.Add(cachedResult);
                }
                else
                {
                    var result = await codeOwnerValidator.ValidateCodeOwnerAsync(username, verbose: false);
                    
                    if (string.IsNullOrEmpty(result.Username))
                    {
                        result.Username = username;
                    }
                    
                    codeOwnerValidationCache[username] = result;
                    validatedOwners.Add(result);
                }
            }
            
            return validatedOwners;
        }

        private async Task ValidateMinimumOwnerRequirements(List<string> serviceOwners, List<string> sourceOwners, string serviceLabel, string path)
        {
            var validatedServiceOwners = await ValidateOwners(serviceOwners);
            var validatedSourceOwners = await ValidateOwners(sourceOwners);

            var validServiceOwnersCount = validatedServiceOwners.Count(owner => owner.IsValidCodeOwner);
            var validSourceOwnersCount = validatedSourceOwners.Count(owner => owner.IsValidCodeOwner);

            var validationErrors = new List<string>();

            if (!string.IsNullOrEmpty(serviceLabel) && validServiceOwnersCount < 2)
                {
                    validationErrors.Add("There must be at least two valid service owners.");
                }
                if (!string.IsNullOrEmpty(path) && validSourceOwnersCount < 2)
                {
                    validationErrors.Add("There must be at least two valid source owners.");
                }

            if (validationErrors.Any())
            {
                throw new InvalidOperationException(string.Join(" ", validationErrors));
            }
        }
    }
}
