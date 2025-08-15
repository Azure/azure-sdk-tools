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
using Azure.Sdk.Tools.Cli.Models.Responses;

namespace Azure.Sdk.Tools.Cli.Tools
{
    [Description("Tool that validates and manipulates codeowners files.")]
    [McpServerToolType]
    public class CodeownersTools(
        IGitHubService githubService,
        IOutputService output,
        ITypeSpecHelper typespecHelper,
        ICodeownersHelper codeownersHelper,
        ICodeownersValidatorHelper codeownersValidator) : MCPTool
    {
        private static Dictionary<string, CodeownersValidationResult> codeownersValidationCache = new Dictionary<string, CodeownersValidationResult>();
        private static readonly string standardServiceCategory = "# Client Libraries";
        private static readonly string standardManagementCategory = "# Management Libraries";

        // URL constants
        private const string azureWriteTeamsBlobUrl = "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/azure-sdk-write-teams-blob";
        private const string githubRawContentBaseUrl = "https://raw.githubusercontent.com";

        // Command names
        private const string updateCodeownersCommandName = "update-codeowners";
        private const string validateCodeownersEntryCommandName = "validate-codeowners-entry";

        // Core command options
        private readonly Option<string> repoOption = new(["--repo", "-r"], "The repository name") { IsRequired = true };
        private readonly Option<string> pathOption = new(["--path", "-p"], "The path for the codeowners entry") { IsRequired = true };
        private readonly Option<string> typeSpecProjectPathOption = new(["--typespec-project"], "Path to typespec project") { IsRequired = true };
        private readonly Option<string> pathOptionOptional = new(["--path", "-p"], "The repository path to check/validate");
        private readonly Option<string> serviceLabelOption = new(["--service-label", "-sl"], "The service label");
        private readonly Option<string[]> serviceOwnersOption = new(["--service-owners", "-so"], "The service owners (space-separated)");
        private readonly Option<string[]> sourceOwnersOption = new(["--source-owners", "-sro"], "The source owners (space-separated)");
        private readonly Option<bool> isAddingOption = new(["--is-adding", "-ia"], "Whether to add (true) or remove (false) owners");
        private readonly Option<string> workingBranchOption = new(["--working-branch", "-wb"], "The branch to make changes on.");
        public override Command GetCommand()
        {
            var command = new Command("codeowners-tools", "A tool to validate and modify codeowners.");
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
                new Command(validateCodeownersEntryCommandName, "Validate codeowners for an existing service entry")
                {
                    repoOption,
                    serviceLabelOption,
                    pathOptionOptional
                }
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
                    var workingBranch = commandParser.GetValueForOption(workingBranchOption);

                    var addResult = await UpdateCodeowners(
                        repoValue ?? "",
                        typespecPathValue ?? "",
                        pathValue ?? "",
                        serviceLabelValue ?? "",
                        serviceOwnersValue?.ToList() ?? new List<string>(),
                        sourceOwnersValue?.ToList() ?? new List<string>(),
                        isAddingValue,
                        workingBranch ?? "");
                    output.Output(addResult);
                    return;
                case validateCodeownersEntryCommandName:
                    var validateRepo = commandParser.GetValueForOption(repoOption);
                    var validateServiceLabel = commandParser.GetValueForOption(serviceLabelOption);
                    var validateRepoPath = commandParser.GetValueForOption(pathOptionOptional);

                    var validateResult = await ValidateCodeownersEntryForService(
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
                if (string.IsNullOrWhiteSpace(serviceLabel) && string.IsNullOrWhiteSpace(path))
                {
                    throw new Exception($"Service label: {serviceLabel} and Path: {path} are both invalid. At least one must be valid");
                }

                // Normalize service path
                var normalizedPath = codeownersHelper.NormalizePath(path);

                // Get labels file contents.
                var labelsFileContent = await githubService.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, Constants.AZURE_COMMON_LABELS_PATH);

                if (labelsFileContent == null)
                {
                    throw new Exception("Could not retrieve labels file from the repository.");
                }

                var labelsContent = labelsFileContent.Content;

                // Validate service label
                if (!string.IsNullOrEmpty(serviceLabel))
                {
                    var serviceLabelValidationResults = LabelHelper.CheckServiceLabel(labelsContent, serviceLabel);
                    if (serviceLabelValidationResults != LabelHelper.ServiceLabelStatus.Exists)
                    {
                        var labelsPullRequests = await githubService.SearchPullRequestsByTitleAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, "Service Label");

                        if (!LabelHelper.CheckServiceLabelInReview(labelsPullRequests, serviceLabel) && string.IsNullOrEmpty(normalizedPath))
                        {
                            throw new Exception($"Service label: {serviceLabel} doesn't exist.");
                        }
                    }
                }

                if (string.IsNullOrEmpty(workingBranch))
                {
                    var codeownersPullRequests = await githubService.SearchPullRequestsByTitleAsync(Constants.AZURE_OWNER_PATH, repo, "CODEOWNERS");

                    foreach (var codeownersPullRequest in codeownersPullRequests)
                    {
                        if (codeownersPullRequest != null &&
                            ((!string.IsNullOrEmpty(serviceLabel) && codeownersPullRequest.Title.Contains(serviceLabel, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrEmpty(normalizedPath) && codeownersPullRequest.Title.Contains(normalizedPath, StringComparison.OrdinalIgnoreCase))))
                        {
                            workingBranch = codeownersPullRequest.Head.Ref;
                        }
                    }
                }

                // Get codeowners file contents.
                var codeownersFileContent = await githubService.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, repo, Constants.AZURE_CODEOWNERS_PATH);

                if (codeownersFileContent == null)
                {
                    throw new Exception("Could not retrieve CODEOWNERS file from the repository.");
                }

                var branchToFetch = string.IsNullOrEmpty(workingBranch) ? "main" : workingBranch;
                var codeownersUrl = $"{githubRawContentBaseUrl}/{Constants.AZURE_OWNER_PATH}/{repo}/{branchToFetch}/{Constants.AZURE_CODEOWNERS_PATH}";

                var codeownersContent = codeownersFileContent.Content;
                var codeownersSha = codeownersFileContent.Sha;

                // Find Codeowner Entry with the validated Label or Path
                bool isMgmtPlane = typespecHelper.IsTypeSpecProjectForMgmtPlane(typeSpecProjectRoot);
                var (startLine, endLine) = isMgmtPlane
                    ? codeownersHelper.FindBlock(codeownersContent, standardManagementCategory)
                    : codeownersHelper.FindBlock(codeownersContent, standardServiceCategory);

                var codeownersEntries = CodeownersParser.ParseCodeownersFile(codeownersUrl, azureWriteTeamsBlobUrl, startLine, endLine);
                for (int i = 0; i < codeownersEntries.Count; i++)
                {
                    (codeownersEntries, i) = codeownersHelper.MergeCodeownerEntries(codeownersEntries, i);
                }

                // Find existing codeowners entry by path or service label
                var matchingEntry = codeownersHelper.FindMatchingEntries(codeownersEntries, normalizedPath, serviceLabel);

                // Update or create the codeowners entry
                var (updatedEntry, codeownersEntryExists) = codeownersHelper.CreateOrUpdateCodeownersEntry(
                    matchingEntry, normalizedPath, serviceLabel, serviceOwners, sourceOwners, isAdding, isMgmtPlane);

                // Validate the modified/created Entry
                var (validationErrors, codeownersValidationResults) = await ValidateMinimumOwnerRequirements(updatedEntry);
                var codeownersValidationResultMessage = string.Join("; ", codeownersValidationResults.Select(r => $"{r.Username}: {(r.IsValidCodeOwner ? "Valid" : "Invalid")}"));
                if (validationErrors.Any())
                {
                    throw new Exception($"{validationErrors} Validation results: {codeownersValidationResultMessage}");
                }

                // Modify the file
                string modifiedCodeownersContent;
                if (codeownersEntryExists)
                {
                    modifiedCodeownersContent = codeownersHelper.ReplaceEntryInLines(codeownersContent, updatedEntry);
                }
                else
                {
                    var updatedEntryWithLines = codeownersHelper.FindAlphabeticalInsertionPoint(codeownersEntries, updatedEntry);
                    modifiedCodeownersContent = codeownersHelper.AddCodeownersEntryAtIndex(codeownersContent, updatedEntryWithLines, updatedEntryWithLines.startLine, codeownersEntryExists);
                }

                // Create Branch, Update File, and Handle PR.
                var actionDescription = isAdding ? "Add codeowner aliases for" : "Remove codeowner aliases for";
                var actionType = isAdding ? "add-codeowner-alias" : "remove-codeowner-alias";

                if (!codeownersEntryExists)
                {
                    actionDescription = "Add codeowner entry for";
                    actionType = "add-codeowner-entry";
                }

                var identifier = !string.IsNullOrWhiteSpace(updatedEntry.ServiceLabels?.FirstOrDefault())
                    ? updatedEntry.ServiceLabels.FirstOrDefault()
                    : updatedEntry.PathExpression;
                var resultMessages = await CreateCodeownersPR(
                    repo,                                                             // Repository name
                    string.Join('\n', modifiedCodeownersContent),                     // Modified content
                    codeownersSha,                                                    // SHA of the file to update 
                    $"{actionDescription} {identifier}", // Description for commit message, PR title, and description
                    actionType,                                             // Branch prefix for the action
                    identifier, // Identifier for the PR
                    workingBranch);

                return string.Join("\n", resultMessages.Concat(new[] { codeownersValidationResultMessage }));
            }
            catch (Exception ex)
            {
                SetFailure();
                return $"Error: {ex}";
            }
        }

        private async Task<List<string>> CreateCodeownersPR(
            string repo,
            string modifiedContent,
            string sha,
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
                branchName = codeownersHelper.CreateBranchName(branchPrefix, identifier);
                var createBranchResult = await githubService.CreateBranchAsync(Constants.AZURE_OWNER_PATH, repo, branchName, "main");
                resultMessages.Add($"Created branch: {branchName} - Status: {createBranchResult}");
            }

            // After branchName is set
            var codeownersFileContent = await githubService.GetContentsAsync(Constants.AZURE_OWNER_PATH, repo, Constants.AZURE_CODEOWNERS_PATH, branchName);
            var codeownersSha = codeownersFileContent.FirstOrDefault()?.Sha;

            // Use codeownersSha in UpdateFileAsync
            await githubService.UpdateFileAsync(Constants.AZURE_OWNER_PATH, repo, Constants.AZURE_CODEOWNERS_PATH, description, modifiedContent, codeownersSha, branchName);

            var prInfoList = await githubService.CreatePullRequestAsync(repo, Constants.AZURE_OWNER_PATH, "main", branchName, "[CODEOWNERS] " + description, description, true);
            if (prInfoList != null)
            {
                resultMessages.Add($"URL: {prInfoList.Url}");
                resultMessages.AddRange(prInfoList.Messages);
            }
            else
            {
                resultMessages.Add("Error: Failed to create pull request. No PR info returned.");
            }

            return resultMessages;
        }

        [McpServerTool(Name = "ValidateCodeownersEntryForService"), Description("Validates codeowners in a specific repository for a given service or repo path.")]
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

                CodeownersEntry? matchingEntry;
                try
                {
                    var codeownersUrl = $"{githubRawContentBaseUrl}/{Constants.AZURE_OWNER_PATH}/{repoName}/main/{Constants.AZURE_CODEOWNERS_PATH}";
                    var codeownersEntries = CodeownersParser.ParseCodeownersFile(codeownersUrl, azureWriteTeamsBlobUrl);
                    matchingEntry = codeownersHelper.FindMatchingEntries(codeownersEntries, path, serviceLabel);
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
                SetFailure();
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
                if (codeownersValidationCache.TryGetValue(username, out var cachedResult))
                {
                    validatedOwners.Add(cachedResult);
                }
                else
                {
                    var result = await codeownersValidator.ValidateCodeOwnerAsync(username, verbose: false);

                    if (string.IsNullOrEmpty(result.Username))
                    {
                        result.Username = username;
                    }

                    codeownersValidationCache[username] = result;
                    validatedOwners.Add(result);
                }
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

            if (validationErrors.Any())
            {
                return (string.Join(" ", validationErrors), allValidationResults);
            }
            return ("", allValidationResults);
        }
    }
}
