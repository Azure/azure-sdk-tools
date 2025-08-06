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
using System.Collections.Concurrent;
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
        private static ConcurrentDictionary<string, CodeOwnerValidationResult> codeOwnerValidationCache = new ConcurrentDictionary<string, CodeOwnerValidationResult>();
        private static readonly string mgmtPlaneCategory = "# Management Plane SDKs";
        private static readonly string dataPlaneCategory = "# Client SDKs";
        private static readonly Dictionary<string, (string RepoName, string ServiceCategory)> azureRepositories = new()
        {
            { "dotnet", ("azure-sdk-for-net", "# ######## Services ########") },
            { "cpp", ("azure-sdk-for-cpp", "# Client SDKs") },
            { "go", ("azure-sdk-for-go", "# SDK (track2)") },
            { "java", ("azure-sdk-for-java", "# ######## Services ########") },
            { "javascript", ("azure-sdk-for-js", "# SDK") },
            { "python", ("azure-sdk-for-python", "# Service team") },
            { "rest-api-specs", ("azure-rest-api-specs", "") },
            { "rust", ("azure-sdk-for-rust", "# Client SDKs") }
        };

        // Command names
        private const string addCodeownersEntryCommandName = "add-codeowners-entry";
        private const string updateCodeownersCommandName = "update-codeowners";
        private const string checkServiceExistsCommandName = "check-service-exists";
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
                new Command(checkServiceExistsCommandName, "Check if a service exists in CODEOWNERS file")
                {
                    repoOption,
                    serviceLabelOption,
                    pathOptionOptional
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
                case checkServiceExistsCommandName:
                    var checkRepo = commandParser.GetValueForOption(repoOption);
                    var checkServiceLabel = commandParser.GetValueForOption(serviceLabelOption);
                    var checkRepoPath = commandParser.GetValueForOption(pathOptionOptional);

                    var existsResult = CheckServiceExistsInCodeowners(
                        checkRepo ?? "",
                        checkServiceLabel,
                        checkRepoPath);
                    output.Output($"Service exists: {existsResult}");
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
                // case mockToolCommandName:
                //     await MockTool();
                //     return;
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

                azureRepositories.TryGetValue(repoName, out var repoInfo);
                var fullRepoName = repoInfo.RepoName;

                var codeownersUrl = $"https://raw.githubusercontent.com/Azure/{fullRepoName}/main/.github/CODEOWNERS";
                var codeownersEntries = CodeownersParser.ParseCodeownersFile(codeownersUrl, "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/azure-sdk-write-teams-blob");
                var matchingEntries = codeownerHelper.FindMatchingEntries(codeownersEntries, serviceLabel, repoPath);

                return (true, matchingEntries, fullRepoName, null);
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error finding service in CODEOWNERS file. Error {ex}";
                logger.LogError(errorMessage);
                return (false, null, null, errorMessage);
            }
        }

        [McpServerTool(Name = "CheckServiceExistsInCodeowners"), Description("Validates code owners in a specific repository for a given service or repo path.")]
        public bool CheckServiceExistsInCodeowners(string repoName, string? serviceLabel = null, string? repoPath = null)
        {
            try
            {
                var (success, matchingEntries, _, errorMessage) = FindCodeownersEntries(repoName, serviceLabel, repoPath);

                if (!success)
                {
                    logger.LogError(errorMessage);
                    return false;
                }

                return matchingEntries != null && matchingEntries.Count > 0;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error in CheckServiceExistsInCodeowners: {ex}");
                return false;
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

                    var codeOwners = await ValidateCodeOwnersConcurrently(uniqueOwners.ToList());
                    response.CodeOwners = codeOwners;
                    response.Message += "Successfully found codeowners.";
                    response.Repository = fullRepoName ?? string.Empty;
                    return response;
                }
                else
                {
                    response.Message += $"Service label '{serviceLabel}' or Repo Path '{repoPath}' not found in {fullRepoName}";
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

        public async Task<List<CodeOwnerValidationResult>> ValidateCodeOwnersConcurrently(List<string> owners)
        {
            var results = new List<CodeOwnerValidationResult>();
            var asyncValidationTasks = new List<Task<CodeOwnerValidationResult>>();

            foreach (var owner in owners)
            {
                var username = owner.TrimStart('@');

                if (codeOwnerValidationCache.TryGetValue(username, out var cachedResult))
                {
                    results.Add(cachedResult);
                }
                else
                {
                    asyncValidationTasks.Add(ValidateCodeOwnerWithCaching(username));
                }
            }

            if (asyncValidationTasks.Count > 0)
            {
                var asyncResults = await Task.WhenAll(asyncValidationTasks);
                results.AddRange(asyncResults);
            }

            return results;
        }

        private async Task<CodeOwnerValidationResult> ValidateCodeOwnerWithCaching(string username)
        {
            var result = await codeOwnerValidator.ValidateCodeOwnerAsync(username, verbose: false);

            if (string.IsNullOrEmpty(result.Username))
            {
                result.Username = username;
            }

            codeOwnerValidationCache.TryAdd(username, result);

            return result;
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
                var response = await setup(repo, inputPath, serviceLabel, serviceOwners, sourceOwners, typeSpecProjectRoot);

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
                var content = response.fileContent.Content;
                var sha = response.fileContent.Sha;

                if (content.Contains(response.path))
                {
                    return $"{response.path} already exists in the CODEOWNERS file.";
                }

                // Step 2: Modify contents logic.
                var (startLine, endLine) = (-1, -1);
                if (response.isMgmtPlane)
                {
                    (startLine, endLine) = codeownerHelper.findBlock(content, mgmtPlaneCategory);
                }
                else
                {
                    (startLine, endLine) = codeownerHelper.findBlock(content, dataPlaneCategory);
                }

                var codeownersEntries = CodeownersParser.ParseCodeownersFile(response.codeownersUrl, "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/azure-sdk-write-teams-blob", startLine, endLine);

                var insertionIndex = codeownerHelper.findAlphabeticalInsertionPoint(codeownersEntries, response.path, response.serviceLabel);

                var formattedCodeownersEntry = codeownerHelper.formatCodeownersEntry(response.path, response.serviceLabel, serviceOwners, sourceOwners);
                var modifiedCodeownersContent = codeownerHelper.addCodeownersEntryAtIndex(content, formattedCodeownersEntry, insertionIndex);

                // Step 3: Create branch, update file, and handle PR
                var resultMessages = await CreateCodeownerPR(
                    response.fullRepoName,                             // Full repository name
                    modifiedCodeownersContent,                         // Modified content
                    sha,                                               // SHA of the file to update
                    $"Add codeowner entry for {response.serviceLabel ?? response.path}", // Commit message
                    $"Add codeowner entry for {response.serviceLabel ?? response.path}", // PR title
                    $"Add codeowner entry for {response.serviceLabel ?? response.path}", // PR description
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
                var response = await setup(repo, inputPath, serviceLabel, serviceOwners, sourceOwners, typeSpecProjectRoot);
                var responseMessages = string.Join("\n", response.ValidationMessages);

                if (!string.IsNullOrEmpty(responseMessages))
                {
                    return responseMessages;
                }

                // Step 1: Get contents.
                var content = response.fileContent.Content;
                var sha = response.fileContent.Sha;

                // Step 2: Find the service category block
                var (startLine, endLine) = codeownerHelper.findBlock(content, response.serviceCategory);

                // Step 3: Parse entries within the block to find the specific entry to modify
                var codeownersEntries = CodeownersParser.ParseCodeownersFile(response.codeownersUrl, "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/azure-sdk-write-teams-blob", startLine, endLine);
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
                    return $"No existing codeowner entry found for service label '{serviceLabel}' or path '{response.path}' in {response.fullRepoName}.";
                }

                // Step 4: Modify the content using the entry's line information
                var lines = content.Split('\n').ToList();

                var modifiedContent = await ModifyCodeownersEntryLines(lines, targetEntry, response.serviceOwners, response.sourceOwners, isAdding);

                // Step 5: Create branch, update file, and handle PR
                var actionDescription = isAdding ? "Add codeowner aliases for" : "Remove codeowner aliases for";
                var actionType = isAdding ? "add-codeowner-alias" : "remove-codeowner-alias";
                
                var resultMessages = await CreateCodeownerPR(
                    response.fullRepoName,                                  // Full repository name
                    string.Join('\n', modifiedContent),                     // Modified content
                    sha,                                                    // SHA of the file to update 
                    $"{actionDescription} {targetEntry.ServiceLabel ?? targetEntry.PathExpression}", // Commit message
                    $"{actionDescription} {targetEntry.ServiceLabel ?? targetEntry.PathExpression}", // PR title
                    $"{actionDescription} {targetEntry.ServiceLabel ?? targetEntry.PathExpression}", // PR description
                    actionType,                                             // Branch prefix for the action
                    targetEntry.ServiceLabel ?? targetEntry.PathExpression, // Identifier for the PR
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
            string fullRepoName,
            string modifiedContent,
            string sha,
            string commitMessage,
            string prTitle,
            string prDescription,
            string branchPrefix,
            string identifier,
            string workingBranch = null)
        {
            List<string> resultMessages = new();
            var branchName = "";

            // Check if we have a working branch from SDK generation
            if (!string.IsNullOrEmpty(workingBranch) && await githubService.GetBranchAsync("Azure", fullRepoName, workingBranch))
            {
                branchName = workingBranch;
                resultMessages.Add($"Using existing branch: {branchName}");
            }
            else
            {
                // Create a new branch only if no working branch exists
                branchName = codeownerHelper.CreateBranchName(branchPrefix, identifier);
                var createBranchResult = await githubService.CreateBranchAsync("Azure", fullRepoName, branchName);
                resultMessages.Add($"Created branch: {branchName} - Status: {createBranchResult}");
            }

            // Update file
            await githubService.UpdateFileAsync("Azure", fullRepoName, ".github/CODEOWNERS", commitMessage, modifiedContent, sha, branchName);

            // Handle PR creation or update existing PR
            var existingPR = await githubService.GetPullRequestForBranchAsync("Azure", fullRepoName, branchName);
            if (existingPR != null)
            {
                // PR already exists - the file update will automatically be added to it
                resultMessages.Add($"Codeowner changes added to existing PR #{existingPR.Number}: {existingPR.HtmlUrl}");
            }
            else
            {
                // No existing PR - create a new one if needed
                var prInfoList = await githubService.CreatePullRequestAsync(fullRepoName, "Azure", "main", branchName, prTitle, prDescription, true);
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
            var modifiedLines = new List<string>(lines);

            if (isAdding)
            {
                var validServiceOwnersToAdd = newServiceOwners?.Where(owner => owner.IsValidCodeOwner).ToList() ?? new List<CodeOwnerValidationResult>();
                var validSourceOwnersToAdd = newSourceOwners?.Where(owner => owner.IsValidCodeOwner).ToList() ?? new List<CodeOwnerValidationResult>();

                // Add owners to the Entry object directly
                if (validServiceOwnersToAdd.Any())
                {
                    foreach (var owner in validServiceOwnersToAdd)
                    {
                        if (!targetEntry.ServiceOwners.Any(existing => existing.TrimStart('@')
                            .Equals(owner.Username.TrimStart('@'), StringComparison.OrdinalIgnoreCase)))
                        {
                            targetEntry.ServiceOwners.Add(owner.Username);
                        }
                    }
                }
                if (validSourceOwnersToAdd.Any())
                {
                    foreach (var owner in validSourceOwnersToAdd)
                    {
                        if (!targetEntry.SourceOwners.Any(existing => existing.TrimStart('@')
                            .Equals(owner.Username.TrimStart('@'), StringComparison.OrdinalIgnoreCase)))
                        {
                            targetEntry.SourceOwners.Add(owner.Username);
                        }
                    }
                }
            }
            else
            {
                var validServiceOwnersToDelete = newServiceOwners?.Select(owner => owner.Username.TrimStart('@')).ToList() ?? new List<string>();
                var validSourceOwnersToDelete = newSourceOwners?.Select(owner => owner.Username.TrimStart('@')).ToList() ?? new List<string>();

                // Delete owners from the Entry object directly
                if (validServiceOwnersToDelete.Any())
                {
                    targetEntry.ServiceOwners.RemoveAll(owner => validServiceOwnersToDelete.Contains(owner.TrimStart('@')));
                }
                if (validSourceOwnersToDelete.Any())
                {
                    targetEntry.SourceOwners.RemoveAll(owner => validSourceOwnersToDelete.Contains(owner.TrimStart('@')));
                }
            }

            var (validatedServiceOwners, validatedSourceOwners) = await validateCodeowners(targetEntry.ServiceOwners, targetEntry.SourceOwners);

            // Validate that the modified entry has at least 2 valid service and source owners
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

            // Generate the new formatted entry for both adding and removing
            var formattedCodeownersEntry = codeownerHelper.formatCodeownersEntry(targetEntry.PathExpression, targetEntry.ServiceLabels[0], targetEntry.ServiceOwners, targetEntry.SourceOwners);

            // Remove the old entry lines
            int originalEntryLineCount = targetEntry.endLine - targetEntry.startLine + 1;
            modifiedLines.RemoveRange(targetEntry.startLine, originalEntryLineCount);

            // Insert the new formatted entry at the same position
            var entryLines = formattedCodeownersEntry.Split('\n');
            modifiedLines.InsertRange(targetEntry.startLine, entryLines);

            return modifiedLines;
        }

        public (bool isValid, string, string) validateRepo(string repo)
        {
            try
            {
                azureRepositories.TryGetValue(repo, out var repoInfo);
                var fullRepoName = repoInfo.RepoName;
                var serviceCategory = repoInfo.ServiceCategory;
                return (true, fullRepoName, serviceCategory);
            }
            catch (Exception ex)
            {
                logger.LogError($"Error: {ex}");
                return (false, "", "");
            }
        }

        public (bool isValid, string) validatePath(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path))
                {
                    path = path.Trim('/');
                    path = $"/{path}/";
                    return (true, path);
                }
                return (false, "");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error: {ex}");
                return (false, "");
            }
        }

        public async Task<(bool isValid, string)> validateServiceLabel(string serviceLabel, IReadOnlyList<RepositoryContent> csvContent)
        {
            try
            {
                List<string> resultMessages = new();

                if (string.IsNullOrEmpty(serviceLabel))
                {
                    return (false, "");
                }

                if (csvContent == null || csvContent.Count == 0)
                {
                    return (false, "");
                }

                else
                {
                    var serviceLabelValidationResults = labelHelper.CheckServiceLabel(csvContent[0].Content, serviceLabel);
                    if (serviceLabelValidationResults != LabelHelper.ServiceLabelStatus.Exists)
                    {
                        var pullRequests = await githubService.SearchPullRequestsByTitleAsync("Azure", "azure-sdk-tools", "Service Label");

                        if (!labelHelper.CheckServiceLabelInReview(pullRequests, serviceLabel))
                        {
                            return (false, "");
                        }
                    }
                }
                return (true, serviceLabel);
            }
            catch (Exception ex)
            {
                logger.LogError($"Error: {ex}");
                return (false, "");
            }
        }

        public async Task<(List<CodeOwnerValidationResult>, List<CodeOwnerValidationResult>)> validateCodeowners(List<string> serviceOwners, List<string> sourceOwners)
        {
            try
            {
                var serviceOwnersValidationResults = await ValidateCodeOwnersConcurrently(serviceOwners);
                var sourceOwnersValidationResults = await ValidateCodeOwnersConcurrently(sourceOwners);

                return (serviceOwnersValidationResults, sourceOwnersValidationResults);
            }
            catch (Exception ex)
            {
                logger.LogError($"Error: {ex}");
                return (new List<CodeOwnerValidationResult>(), new List<CodeOwnerValidationResult>());
            }
        }

        public async Task<CodeownerWorkflowResponse> setup(
            string repo,
            string inputPath,
            string inputServiceLabel,
            List<string> inputtedServiceOwners,
            List<string> inputtedSourceOwners,
            string typeSpecProjectRoot)
        {
            List<string> resultMessages = new();
            var response = new CodeownerWorkflowResponse();

            // Validate Repo
            var (validRepo, fullRepoName, serviceCategory) = validateRepo(repo);
            if (!validRepo)
            {
                resultMessages.Add($"Repo path: {repo} is invalid.");
            }
            else
            {
                response.fullRepoName = fullRepoName;
                response.serviceCategory = serviceCategory;
            }

            // Validate path
            var (validPath, path) = validatePath(inputPath);
            if (!validPath)
            {
                resultMessages.Add($"Path: {inputPath} is invalid.");
            }
            else
            {
                response.path = path;
            }

            // Get common labels file.
            var csvContent = await githubService.GetContentsAsync("Azure", "azure-sdk-tools", "tools/github/data/common-labels.csv");
            if (csvContent == null || csvContent.Count == 0)
            {
                resultMessages.Add("Could not retrieve common labels file.");
            }

            // Validate service label
            var (validServiceLabel, serviceLabel) = await validateServiceLabel(inputServiceLabel, csvContent);
            if (!validServiceLabel)
            {
                resultMessages.Add($"Service label: {inputServiceLabel} is invalid.");
            }
            else
            {
                response.serviceLabel = serviceLabel;
            }

            // Validate owners
            (response.serviceOwners, response.sourceOwners) = await validateCodeowners(inputtedServiceOwners, inputtedSourceOwners);

            // Check if it's management plane.
            response.isMgmtPlane = typespecHelper.IsTypeSpecProjectForMgmtPlane(typeSpecProjectRoot);

            // Get CODEOWNERS file contents.
            var fileContent = await githubService.GetContentsAsync("Azure", fullRepoName, ".github/CODEOWNERS");

            if (fileContent == null || fileContent.Count == 0)
            {
                resultMessages.Add($"Could not retrieve CODEOWNERS file");
            }
            else
            {
                response.fileContent = fileContent[0];
            }

            response.codeownersUrl = $"https://raw.githubusercontent.com/Azure/{fullRepoName}/main/.github/CODEOWNERS";
            response.ValidationMessages = resultMessages;

            return response;
        }
        public class CodeownerWorkflowResponse
        {
            public string fullRepoName { get; set; }
            public string serviceCategory { get; set; }
            public string path { get; set; }
            public string serviceLabel { get; set; }
            public List<CodeOwnerValidationResult> serviceOwners { get; set; }
            public List<CodeOwnerValidationResult> sourceOwners { get; set; }
            public bool isMgmtPlane { get; set; }
            public string codeownersUrl { get; set; }
            public RepositoryContent fileContent { get; set; }
            public List<string> ValidationMessages { get; set; }
        }

        // [McpServerTool(Name = "mocktool"), Description("Does mock stuff.")]
        // public async Task MockTool()
        // {
        //     try
        //     {
        //         var fileContent = await githubService.GetContentsAsync("Azure", "azure-sdk-for-net", ".github/CODEOWNERS");

        //         if (fileContent == null || fileContent.Count == 0)
        //         {
        //             logger.LogError("Could not retrieve CODEOWNERS file");
        //             return;
        //         }

        //         var content = fileContent[0].Content;
        //         var (startLine, endLine) = codeownerHelper.findBlock(content, "# ######## Services ########");
        //         logger.LogInformation($"start = {startLine}, end = {endLine}");

        //         var codeownersUrl = $"https://raw.githubusercontent.com/Azure/azure-sdk-for-net/main/.github/CODEOWNERS";
        //         var codeownersEntries = CodeownersParser.ParseCodeownersFile(codeownersUrl, startLine : startLine, endLine : endLine);

        //         for (int i = 0; i < codeownersEntries.Count; i++)
        //         {
        //             (codeownersEntries, i) = codeownerHelper.mergeCodeownerEntries(codeownersEntries, i);
        //         }

        //         // Create our custom comparer and sort the entries
        //         var comparer = new CodeownersEntryPathComparer();
        //         var sortedEntries = codeownersEntries.OrderBy(entry => entry, comparer).ToList();

        //         // Write the sorted entries to a new file
        //         var outputPath = "./Tools/CodeownerTools/CODEOWNER_EDITED";
        //         var outputLines = new List<string>();

        //         var lines = content.Split('\n');

        //         for (int i = 0; i < startLine + 2; i++)
        //         {
        //             outputLines.Add(lines[i]);
        //         }

        //         foreach (var entry in sortedEntries)
        //         {
        //             var formattedEntry = entry.FormatCodeownersEntry();
        //             if (!string.IsNullOrWhiteSpace(formattedEntry))
        //             {
        //                 outputLines.Add(formattedEntry);
        //                 outputLines.Add("");
        //             }
        //         }

        //         for (int i = endLine; i < lines.Length; i++)
        //         {
        //             outputLines.Add(lines[i]);
        //         }

        //         // Write all lines to the file
        //         await File.WriteAllLinesAsync(outputPath, outputLines);

        //         logger.LogInformation($"Successfully wrote {sortedEntries.Count} sorted entries to {outputPath}");
        //     }
        //     catch (Exception ex)
        //     {
        //         logger.LogInformation($"Error: {ex}");
        //     }
        // }
    }
}
