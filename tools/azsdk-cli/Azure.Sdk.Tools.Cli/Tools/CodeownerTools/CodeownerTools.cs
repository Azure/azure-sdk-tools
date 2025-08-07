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
        private const string addCodeownersEntryCommandName = "add-codeowners-entry";
        private const string updateCodeownersCommandName = "update-codeowners";
        private const string validateCodeOwnerEntryCommandName = "validate-codeowner-entry";
        private const string mockToolCommandName = "mock-tool";

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
                new Command(addCodeownersEntryCommandName, "Add a codeowners entry to a repository")
                {
                    repoOption,
                    pathOption,
                    serviceLabelOption,
                    serviceOwnersOption,
                    sourceOwnersOption,
                    typeSpecProjectPathOption,
                    workingBranchOption
                },
                new Command(updateCodeownersCommandName, "Update codeowners in a repository")
                {
                    repoOption,
                    pathOption,
                    serviceLabelOption,
                    serviceOwnersOption,
                    sourceOwnersOption,
                    typeSpecProjectPathOption,
                    isAddingOption,
                    workingBranchOption
                },
                new Command(validateCodeOwnerEntryCommandName, "Validate code owners for an existing service entry")
                {
                    repoOption,
                    serviceLabelOption,
                    pathOptionOptional
                },
                new Command(mockToolCommandName, "Mock tool for testing purposes")
                {
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
                case addCodeownersEntryCommandName:
                    var repoValue = commandParser.GetValueForOption(repoOption);
                    var pathValue = commandParser.GetValueForOption(pathOption);
                    var serviceLabelValue = commandParser.GetValueForOption(serviceLabelOption);
                    var serviceOwnersValue = commandParser.GetValueForOption(serviceOwnersOption);
                    var sourceOwnersValue = commandParser.GetValueForOption(sourceOwnersOption);
                    var typespecPathValue = commandParser.GetValueForOption(typeSpecProjectPathOption);
                    var workingBranchValue = commandParser.GetValueForOption(workingBranchOption);

                    var addResult = await AddCodeownerEntry(
                        repoValue ?? "",
                        pathValue ?? "",
                        serviceLabelValue ?? "",
                        serviceOwnersValue?.ToList() ?? new List<string>(),
                        sourceOwnersValue?.ToList() ?? new List<string>(),
                        typespecPathValue ?? "",
                        workingBranchValue ?? "");
                    output.Output(addResult);
                    return;
                case updateCodeownersCommandName:
                    var repoValue2 = commandParser.GetValueForOption(repoOption);
                    var pathValue2 = commandParser.GetValueForOption(pathOption);
                    var serviceLabelValue2 = commandParser.GetValueForOption(serviceLabelOption);
                    var serviceOwnersValue2 = commandParser.GetValueForOption(serviceOwnersOption);
                    var sourceOwnersValue2 = commandParser.GetValueForOption(sourceOwnersOption);
                    var typespecPathValue2 = commandParser.GetValueForOption(typeSpecProjectPathOption);
                    var isAddingValue = commandParser.GetValueForOption(isAddingOption);
                    var workingBranchValue2 = commandParser.GetValueForOption(workingBranchOption);

                    var addResult2 = await UpdateCodeowners(
                        repoValue2 ?? "",
                        pathValue2 ?? "",
                        serviceLabelValue2 ?? "",
                        serviceOwnersValue2?.ToList() ?? new List<string>(),
                        sourceOwnersValue2?.ToList() ?? new List<string>(),
                        typespecPathValue2 ?? "",
                        isAddingValue,
                        workingBranchValue2 ?? "");
                    output.Output(addResult2);
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

        /// <summary>
        /// Helper method to find matching entries in CODEOWNERS file for a given service or path
        /// </summary>
        private (bool success, List<CodeownersEntry?>? matchingEntries, string? fullRepoName, string? errorMessage) FindCodeownersEntries(string repoName, string? serviceLabel = null, string? repoPath = null)
        {
            try
            {
                if (string.IsNullOrEmpty(serviceLabel) && string.IsNullOrEmpty(repoPath))
                {
                    return (false, null, null, "Must provide a service label or a repository path.");
                }

                var codeownersUrl = $"{githubRawContentBaseUrl}/Azure/{repoName}/main/.github/CODEOWNERS";
                var codeownersEntries = CodeownersParser.ParseCodeownersFile(codeownersUrl, azureWriteTeamsBlobUrl);
                var matchingEntries = codeownerHelper.FindMatchingEntries(codeownersEntries, serviceLabel, repoPath);

                return (true, matchingEntries, repoName, null);
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error finding service in CODEOWNERS file. Error {ex}";
                logger.LogError(errorMessage);
                return (false, null, null, errorMessage);
            }
        }

        [McpServerTool(Name = "ValidateCodeOwnerEntryForService"), Description("Validates code owners in a specific repository for a given service or repo path.")]
        public async Task<ServiceCodeOwnerResult> ValidateCodeOwnerEntryForService(string repoName, string? serviceLabel = null, string? repoPath = null)
        {
            ServiceCodeOwnerResult response = new() { };

            try
            {
                var (success, matchingEntries, fullRepoName, errorMessage) = FindCodeownersEntries(repoName, serviceLabel, repoPath);

                if (!success)
                {
                    response.Message += errorMessage ?? "Unknown error occurred";
                    return response;
                }

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

        [McpServerTool(Name = "AddCodeownerEntry"), Description("Adds a codeowner entry for a given service label or path for a repo.")]
        public async Task<string> AddCodeownerEntry(
            string repo,
            string inputPath,
            string serviceLabel,
            List<string> serviceOwners,
            List<string> sourceOwners,
            string typeSpecProjectRoot,
            string workingBranch = null)
        {
            try
            {
                // Step 0: Validation.
                var response = await PrepareCodeownerRequest(repo, inputPath, serviceLabel, serviceOwners, sourceOwners, typeSpecProjectRoot);

                // Check that we have enough valid owners
                var validServiceOwners = response.serviceOwners?.Where(owner => owner.IsValidCodeOwner).ToList() ?? new List<CodeOwnerValidationResult>();
                var validSourceOwners = response.sourceOwners?.Where(owner => owner.IsValidCodeOwner).ToList() ?? new List<CodeOwnerValidationResult>();

                if (!string.IsNullOrEmpty(response.serviceLabel) && validServiceOwners.Count < 2)
                {
                    response.ValidationMessages.Add("There must be at least two valid service owners.");
                }
                if (!string.IsNullOrEmpty(response.path) && validSourceOwners.Count < 2)
                {
                    response.ValidationMessages.Add("There must be at least two valid source owners.");
                }

                var responseMessages = string.Join("\n", response.ValidationMessages);
                if (!string.IsNullOrEmpty(responseMessages))
                {
                    return responseMessages;
                }

                // Step 1: Get contents.
                var content = response.fileContent;
                var sha = response.Sha;

                if (content.Contains(response.path))
                {
                    return $"{response.path} already exists in the CODEOWNERS file.";
                }

                // Step 2: Modify contents logic.
                var (startLine, endLine) = (-1, -1);
                if (response.isMgmtPlane)
                {
                    (startLine, endLine) = codeownerHelper.findBlock(content, standardManagementCategory);
                }
                else
                {
                    (startLine, endLine) = codeownerHelper.findBlock(content, standardServiceCategory);
                }

                var codeownersEntries = CodeownersParser.ParseCodeownersFile(response.codeownersUrl, azureWriteTeamsBlobUrl, startLine, endLine);

                var insertionIndex = codeownerHelper.findAlphabeticalInsertionPoint(codeownersEntries, response.path, response.serviceLabel);

                var formattedCodeownersEntry = codeownerHelper.formatCodeownersEntry(response.path, response.serviceLabel, serviceOwners, sourceOwners);
                var modifiedCodeownersContent = codeownerHelper.addCodeownersEntryAtIndex(content, formattedCodeownersEntry, insertionIndex);

                // Step 3: Create branch, update file, and handle PR
                var resultMessages = await CreateCodeownerPR(
                    repo,                                              // Repository name
                    modifiedCodeownersContent,                         // Modified content
                    sha,                                               // SHA of the file to update
                    $"Add codeowner entry for {response.serviceLabel ?? response.path}", // Description for commit message, PR title, and description
                    "add-codeowner-entry",                             // Branch prefix for the action
                    response.serviceLabel ?? response.path,            // Identifier for the PR
                    workingBranch);

                return string.Join("\n", resultMessages);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error in AddCodeownerEntry");
                return $"Error: {ex.Message}";
            }
        }

        [McpServerTool(Name = "UpdateCodeowners"), Description("Adds or deletes codeowners for a given service label or path in a repo.")]
        public async Task<string> UpdateCodeowners(
            string repo,
            string inputPath,
            string serviceLabel,
            List<string> serviceOwners,
            List<string> sourceOwners,
            string typeSpecProjectRoot,
            bool isAdding,
            string workingBranch = null)
        {
            try
            {
                // Step 0: Validation
                var response = await PrepareCodeownerRequest(repo, inputPath, serviceLabel, serviceOwners, sourceOwners, typeSpecProjectRoot);
                var responseMessages = string.Join("\n", response.ValidationMessages);

                if (!string.IsNullOrEmpty(responseMessages))
                {
                    return responseMessages;
                }

                // Step 1: Get contents.
                var content = response.fileContent;
                var sha = response.Sha;

                // Step 2: Find the service category block
                var (startLine, endLine) = codeownerHelper.findBlock(content, standardServiceCategory);

                // Step 3: Parse entries within the block to find the specific entry to modify
                var codeownersEntries = CodeownersParser.ParseCodeownersFile(response.codeownersUrl, azureWriteTeamsBlobUrl, startLine, endLine);
                for (int i = 0; i < codeownersEntries.Count; i++)
                {
                    (codeownersEntries, i) = codeownerHelper.mergeCodeownerEntries(codeownersEntries, i);
                }

                CodeownersEntry? targetEntry = null;
                if (!string.IsNullOrEmpty(response.path))
                {
                    // get the first entry that matches if no one get null. Then compare the path of the entry with the given path 
                    targetEntry = codeownersEntries.FirstOrDefault(entry =>
                        entry.PathExpression?.Equals(response.path, StringComparison.OrdinalIgnoreCase) == true);
                }
                else if (!string.IsNullOrEmpty(response.serviceLabel))
                {   // search for service label in the service labels of the entries
                    targetEntry = codeownersEntries.FirstOrDefault(entry =>
                        entry.ServiceLabels?.Any(label => label.Equals(response.serviceLabel, StringComparison.OrdinalIgnoreCase)) == true);
                }

                if (targetEntry == null)
                {
                    return $"No existing codeowner entry found for service label '{serviceLabel}' or path '{response.path}' in {repo}.";
                }

                // Step 4: Modify the content using the entry's line information
                var lines = content.Split('\n').ToList();

                var modifiedContent = await ModifyCodeownersEntryLines(lines, targetEntry, response.serviceOwners, response.sourceOwners, isAdding);

                // Step 5: Create branch, update file, and handle PR
                var actionDescription = isAdding ? "Add codeowner aliases for" : "Remove codeowner aliases for";
                var actionType = isAdding ? "add-codeowner-alias" : "remove-codeowner-alias";
                
                var resultMessages = await CreateCodeownerPR(
                    repo,                                              // Repository name
                    string.Join('\n', modifiedContent),                     // Modified content
                    sha,                                                    // SHA of the file to update 
                    $"{actionDescription} {targetEntry.ServiceLabels?.FirstOrDefault() ?? targetEntry.PathExpression}", // Description for commit message, PR title, and description
                    actionType,                                             // Branch prefix for the action
                    targetEntry.ServiceLabels?.FirstOrDefault() ?? targetEntry.PathExpression, // Identifier for the PR
                    workingBranch);

                return string.Join("\n", resultMessages);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in UpdateCodeowners");
                return $"Error: {ex.Message}";
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
            if (!string.IsNullOrEmpty(workingBranch) && await githubService.GetBranchAsync("Azure", repo, workingBranch))
            {
                branchName = workingBranch;
                resultMessages.Add($"Using existing branch: {branchName}");
            }
            else
            {
                // Create a new branch only if no working branch exists
                branchName = codeownerHelper.CreateBranchName(branchPrefix, identifier);
                var createBranchResult = await githubService.CreateBranchAsync("Azure", repo, branchName);
                resultMessages.Add($"Created branch: {branchName} - Status: {createBranchResult}");
            }

            // Update file
            await githubService.UpdateFileAsync("Azure", repo, ".github/CODEOWNERS", description, modifiedContent, sha, branchName);

            // Handle PR creation or update existing PR
            var existingPR = await githubService.GetPullRequestForBranchAsync("Azure", repo, branchName);
            if (existingPR != null)
            {
                // PR already exists - the file update will automatically be added to it
                resultMessages.Add($"Codeowner changes added to existing PR #{existingPR.Number}: {existingPR.HtmlUrl}");
            }
            else
            {
                // No existing PR - create a new one if needed
                var prInfoList = await githubService.CreatePullRequestAsync(repo, "Azure", "main", branchName, description, description, true);
                resultMessages.AddRange(prInfoList);
            }

            return resultMessages;
        }

        private async Task<List<string>> ModifyCodeownersEntryLines(
            List<string> lines,
            CodeownersEntry targetEntry,
            List<CodeOwnerValidationResult> newServiceOwners,
            List<CodeOwnerValidationResult> newSourceOwners,
            bool isAdding)
        {
            // Modify the target entry's owners
            if (isAdding)
            {
                codeownerHelper.AddUniqueOwners(targetEntry.ServiceOwners, newServiceOwners?.Where(owner => owner.IsValidCodeOwner).ToList() ?? new List<CodeOwnerValidationResult>());
                codeownerHelper.AddUniqueOwners(targetEntry.SourceOwners, newSourceOwners?.Where(owner => owner.IsValidCodeOwner).ToList() ?? new List<CodeOwnerValidationResult>());
            }
            else
            {
                codeownerHelper.RemoveOwners(targetEntry.ServiceOwners, newServiceOwners?.Select(owner => owner.Username).ToList() ?? new List<string>());
                codeownerHelper.RemoveOwners(targetEntry.SourceOwners, newSourceOwners?.Select(owner => owner.Username).ToList() ?? new List<string>());
            }

            // Validate the modified entry has minimum required owners
            await ValidateMinimumOwnerRequirements(targetEntry);

            // Replace the entry in the file lines
            return codeownerHelper.ReplaceEntryInLines(lines, targetEntry);
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

        private async Task ValidateMinimumOwnerRequirements(CodeownersEntry targetEntry)
        {
            var validatedServiceOwners = await ValidateOwners(targetEntry.ServiceOwners);
            var validatedSourceOwners = await ValidateOwners(targetEntry.SourceOwners);

            var validServiceOwnersCount = validatedServiceOwners.Count(owner => owner.IsValidCodeOwner);
            var validSourceOwnersCount = validatedSourceOwners.Count(owner => owner.IsValidCodeOwner);

            var validationErrors = new List<string>();

            if (validServiceOwnersCount < 2)
            {
                validationErrors.Add($"Modified entry must have at least 2 valid service owners. Current count: {validServiceOwnersCount}.");
            }

            if (validSourceOwnersCount < 2)
            {
                validationErrors.Add($"Modified entry must have at least 2 valid source owners. Current count: {validSourceOwnersCount}.");
            }

            if (validationErrors.Any())
            {
                throw new InvalidOperationException(string.Join(" ", validationErrors));
            }
        }

        public async Task<CodeownerWorkflowResponse> PrepareCodeownerRequest(
            string repo,
            string inputPath,
            string inputServiceLabel,
            List<string> inputtedServiceOwners,
            List<string> inputtedSourceOwners,
            string typeSpecProjectRoot)
        {
            List<string> resultMessages = new();
            var response = new CodeownerWorkflowResponse();

            // Validate Repo - consolidated validation logic
            try
            {
                if (string.IsNullOrEmpty(repo))
                {
                    resultMessages.Add($"Repo path: {repo} is invalid.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error validating repo: {ex}");
                resultMessages.Add($"Repo path: {repo} is invalid.");
            }

            // Validate path - consolidated validation logic
            try
            {
                if (!string.IsNullOrEmpty(inputPath))
                {
                    var path = inputPath.Trim('/');
                    path = $"/{path}/";
                    response.path = path;
                }
                else
                {
                    resultMessages.Add($"Path: {inputPath} is invalid.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error validating path: {ex}");
                resultMessages.Add($"Path: {inputPath} is invalid.");
            }

            // Get common labels file.
            var csvContent = await githubService.GetContentsAsync("Azure", "azure-sdk-tools", "tools/github/data/common-labels.csv");
            if (csvContent == null || csvContent.Count == 0)
            {
                resultMessages.Add("Could not retrieve common labels file.");
            }

            // Validate service label - consolidated validation logic
            try
            {
                if (!string.IsNullOrEmpty(inputServiceLabel) && csvContent != null && csvContent.Count > 0)
                {
                    var serviceLabelValidationResults = labelHelper.CheckServiceLabel(csvContent[0].Content, inputServiceLabel);
                    if (serviceLabelValidationResults != LabelHelper.ServiceLabelStatus.Exists)
                    {
                        var pullRequests = await githubService.SearchPullRequestsByTitleAsync("Azure", "azure-sdk-tools", "Service Label");

                        if (!labelHelper.CheckServiceLabelInReview(pullRequests, inputServiceLabel))
                        {
                            resultMessages.Add($"Service label: {inputServiceLabel} is invalid.");
                        }
                        else
                        {
                            response.serviceLabel = inputServiceLabel;
                        }
                    }
                    else
                    {
                        response.serviceLabel = inputServiceLabel;
                    }
                }
                else if (!string.IsNullOrEmpty(inputServiceLabel))
                {
                    resultMessages.Add($"Service label: {inputServiceLabel} is invalid.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error validating service label: {ex}");
                resultMessages.Add($"Service label: {inputServiceLabel} is invalid.");
            }

            // Validate owners - consolidated validation logic
            try
            {
                var serviceOwnersValidationResults = await ValidateOwners(inputtedServiceOwners);
                var sourceOwnersValidationResults = await ValidateOwners(inputtedSourceOwners);

                response.serviceOwners = serviceOwnersValidationResults;
                response.sourceOwners = sourceOwnersValidationResults;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error validating codeowners: {ex}");
                response.serviceOwners = new List<CodeOwnerValidationResult>();
                response.sourceOwners = new List<CodeOwnerValidationResult>();
            }

            // Check if it's management plane.
            response.isMgmtPlane = typespecHelper.IsTypeSpecProjectForMgmtPlane(typeSpecProjectRoot);

            // Get CODEOWNERS file contents.
            var fileContent = await githubService.GetContentsAsync("Azure", repo, ".github/CODEOWNERS");

            if (fileContent == null || fileContent.Count == 0)
            {
                resultMessages.Add($"Could not retrieve CODEOWNERS file with repository path 'Azure/{repo}/.github/CODEOWNERS'");
            }
            else
            {
                response.fileContent = fileContent[0].Content;
                response.Sha = fileContent[0].Sha;
            }

            response.codeownersUrl = $"{githubRawContentBaseUrl}/Azure/{repo}/main/.github/CODEOWNERS";
            response.ValidationMessages = resultMessages;

            return response;
        }
    }
}
