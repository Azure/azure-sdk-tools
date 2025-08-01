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
        ICodeOwnerHelper codeownerHelper,
        ICodeOwnerValidatorHelper codeOwnerValidator,
        ILabelHelper labelHelper) : MCPTool
    {
        private static ConcurrentDictionary<string, CodeOwnerValidationResult> codeOwnerValidationCache = new ConcurrentDictionary<string, CodeOwnerValidationResult>();
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
        private const string addCodeownersCommandName = "add-codeowners";
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
                    workingBranchOption
                },
                new Command(addCodeownersCommandName, "Add codeowners to a repository")
                {
                    repoOption,
                    pathOption,
                    serviceLabelOption,
                    serviceOwnersOption,
                    sourceOwnersOption,
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
                    var workingBranchValue = commandParser.GetValueForOption(workingBranchOption);

                    var addResult = await AddCodeownerEntry(
                        repoValue ?? "",
                        pathValue ?? "",
                        serviceLabelValue ?? "",
                        serviceOwnersValue?.ToList() ?? new List<string>(),
                        sourceOwnersValue?.ToList() ?? new List<string>(),
                        workingBranchValue ?? "");
                    output.Output(addResult);
                    return;
                case addCodeownersCommandName:
                    var repoValue2 = commandParser.GetValueForOption(repoOption);
                    var pathValue2 = commandParser.GetValueForOption(pathOption);
                    var serviceLabelValue2 = commandParser.GetValueForOption(serviceLabelOption);
                    var serviceOwnersValue2 = commandParser.GetValueForOption(serviceOwnersOption);
                    var sourceOwnersValue2 = commandParser.GetValueForOption(sourceOwnersOption);
                    var workingBranchValue2 = commandParser.GetValueForOption(workingBranchOption);

                    var addResult2 = await AddCodeowners(
                        repoValue2 ?? "",
                        pathValue2 ?? "",
                        serviceLabelValue2 ?? "",
                        serviceOwnersValue2?.ToList() ?? new List<string>(),
                        sourceOwnersValue2?.ToList() ?? new List<string>(),
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

        private (List<CodeOwnerValidationResult> validOwners, List<string> invalidOwners) ValidateOwnerGroup(
            List<CodeOwnerValidationResult> validationResults, 
            List<string> ownerGroup)
        {
            var ownerValidationResults = validationResults
                .Where(result => ownerGroup.Contains(result.Username.TrimStart('@'), StringComparer.OrdinalIgnoreCase))
                .ToList();

            var validOwners = ownerValidationResults.Where(result => result.IsValidCodeOwner).ToList();
            var invalidOwners = ownerValidationResults.Where(result => !result.IsValidCodeOwner)
                .Select(r => r.Username).ToList();

            return (validOwners, invalidOwners);
        }

        [McpServerTool(Name = "AddCodeownerEntry"), Description("Adds a codeowner entry for a given service label or path for a repo.")]
        public async Task<string> AddCodeownerEntry(
            string repo,
            string path,
            string serviceLabel,
            List<string> serviceOwners,
            List<string> sourceOwners,
            string workingBranch = null)
        {
            try
            {
                var resultMessages = new List<string>();

                // Step 0: Validation.
                var isValid = true;
                // Validate service label
                if (string.IsNullOrEmpty(serviceLabel))
                {
                    resultMessages.Add($"Service label {serviceLabel} is null");
                    isValid = false;
                }

                // Get CSV content from GitHub
                var csvContent = await githubService.GetContentsAsync("Azure", "azure-sdk-tools", "tools/github/data/common-labels.csv");
                if (csvContent == null || csvContent.Count == 0)
                {
                    resultMessages.Add("Could not retrieve service labels for validation.");
                    isValid = false;
                }
                else
                {
                    var serviceLabelValidationResults = labelHelper.CheckServiceLabel(csvContent[0].Content, serviceLabel);
                    if (serviceLabelValidationResults != LabelHelper.ServiceLabelStatus.Exists)
                    {
                        var pullRequests = await githubService.SearchPullRequestsByTitleAsync("Azure", "azure-sdk-tools", "Service Label");

                        if (labelHelper.CheckServiceLabelInReview(pullRequests, serviceLabel))
                        {
                            resultMessages.Add($"{serviceLabel} is currently in review.");
                        }
                        else
                        {
                            resultMessages.Add($"The service label {serviceLabel} does not exist.");
                            isValid = false;
                        }
                    }
                }

                // Normalize path format - ensure it starts and ends with /
                if (!string.IsNullOrEmpty(path))
                {
                    path = path.Trim('/');
                    path = $"/{path}/";
                }

                // Validate codeowners
                var allOwners = serviceOwners.Concat(sourceOwners).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var codeownersValidationResults = await ValidateCodeOwnersConcurrently(allOwners);

                var (validSourceOwners, invalidSourceOwners) = ValidateOwnerGroup(codeownersValidationResults, sourceOwners);
                var (validServiceOwners, invalidServiceOwners) = ValidateOwnerGroup(codeownersValidationResults, serviceOwners);

                if (invalidSourceOwners.Any())
                {
                    resultMessages.Add($"Invalid source owners: {string.Join(", ", invalidSourceOwners)}");
                }

                if (!string.IsNullOrEmpty(path) && validSourceOwners.Count < 2)
                {
                    resultMessages.Add("There must be at least two valid source owners.");
                    isValid = false;
                }

                if (invalidServiceOwners.Any())
                {
                    resultMessages.Add($"Invalid service owners: {string.Join(", ", invalidServiceOwners)}");
                }
                
                if (!string.IsNullOrEmpty(serviceLabel) && validServiceOwners.Count < 2)
                {
                    resultMessages.Add("There must be at least two valid service owners.");
                    isValid = false;
                }

                if (!isValid)
                {
                    return string.Join("\n", resultMessages);
                }

                // Step 1: Get contents.
                azureRepositories.TryGetValue(repo, out var repoInfo);
                var fullRepoName = repoInfo.RepoName;
                var serviceCategory = repoInfo.ServiceCategory;

                var codeownersUrl = $"https://raw.githubusercontent.com/Azure/{fullRepoName}/main/.github/CODEOWNERS";
                var fileContent = await githubService.GetContentsAsync("Azure", fullRepoName, ".github/CODEOWNERS");

                if (fileContent == null || fileContent.Count == 0)
                {
                    return $"Could not retrieve CODEOWNERS file";
                }

                var content = fileContent[0].Content;
                var sha = fileContent[0].Sha;

                // Validate path????
                if (content.Contains(path))
                {
                    return $"{path} already exists in the CODEOWNERS file.";
                }

                // Step 2: Modify contents logic.
                var (startLine, endLine) = codeownerHelper.findBlock(content, serviceCategory);

                var codeownersEntries = CodeownersParser.ParseCodeownersFile(codeownersUrl, "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/azure-sdk-write-teams-blob", startLine, endLine);

                var insertionIndex = codeownerHelper.findAlphabeticalInsertionPoint(codeownersEntries, path, serviceLabel);

                var formattedCodeownersEntry = codeownerHelper.formatCodeownersEntry(path, serviceLabel, serviceOwners, sourceOwners);
                var modifiedCodeownersContent = codeownerHelper.addCodeownersEntryAtIndex(content, formattedCodeownersEntry, insertionIndex);

                // Step 3: Use existing branch or create new one, then update file.
                var branchName = "";
                
                // Check if we have a working branch from SDK generation
                if (!string.IsNullOrEmpty(workingBranch) && await githubService.GetBranchAsync("Azure", fullRepoName, workingBranch))
                {
                    // Reuse the existing branch from SDK generation
                    branchName = workingBranch;
                    var createBranchInfo = $"Using existing branch: {branchName}";
                    resultMessages.Add(createBranchInfo);
                }
                else
                {
                    // Create a new branch only if no working branch exists
                    branchName = codeownerHelper.CreateBranchName("add-codeowner-entry", path ?? serviceLabel);
                    var createBranchInfo = await githubService.CreateBranchAsync("Azure", fullRepoName, branchName);
                    resultMessages.Add($"{createBranchInfo}");
                }
                
                await githubService.UpdateFileAsync("Azure", fullRepoName, ".github/CODEOWNERS", $"Add codeowner entry for {path ?? serviceLabel}", modifiedCodeownersContent, sha, branchName);

                // Step 4: Handle PR creation or update existing PR
                var existingPR = await githubService.GetPullRequestForBranchAsync("Azure", fullRepoName, branchName);
                if (existingPR != null)
                {
                    // PR already exists - the file update will automatically be added to it
                    resultMessages.Add($"Codeowner changes added to existing PR #{existingPR.Number}: {existingPR.HtmlUrl}");
                }
                else
                {
                    // No existing PR - create a new one if needed
                    var prInfoList = await githubService.CreatePullRequestAsync(fullRepoName, "Azure", "main", branchName, $"Add codeowner entry for {path ?? serviceLabel}", $"Add codeowner entry for {path ?? serviceLabel}", true);
                    resultMessages.AddRange(prInfoList);
                }

                return string.Join("\n", resultMessages);
            }
            catch (Exception ex)
            {
                logger.LogError($"{ex}");
                return $"Error: {ex}";
            }
        }

        [McpServerTool(Name = "AddCodeowners"), Description("Adds codeowners to a given service label or path for a repo.")]
        public async Task<string> AddCodeowners(
            string repo,
            string path,
            string serviceLabel,
            List<string> serviceOwners,
            List<string> sourceOwners,
            string workingBranch)
        {
            try
            {
                return "";
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in AddCodeowners");
                return null;
            }
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
        //         var codeownersEntries = CodeownersParser.ParseCodeownersFile(codeownersUrl, startLine : 310, endLine : 330);

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
        //             logger.LogInformation($" owners : {entry.ServiceOwners} ");
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

        /// Helper method to check if a line matches a specific service entry
        private bool DoesLineMatchServiceEntry(string line, CodeownersEntry entry)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                return false;

            var pathPart = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return pathPart != null && pathPart.Equals(entry.PathExpression, StringComparison.OrdinalIgnoreCase);
        }

        private string RemoveOwnersFromLine(string line, List<string> ownersToRemove)
        {
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (parts.Count == 0)
                return line;

            var pathPart = parts[0];
            var ownerParts = parts.Skip(1).ToList();

            var normalizedOwnersToRemove = ownersToRemove.Select(o => o.TrimStart('@').ToLowerInvariant()).ToList();
            var remainingOwners = ownerParts.Where(owner =>
                !normalizedOwnersToRemove.Contains(owner.TrimStart('@').ToLowerInvariant())).ToList();

            return $"{pathPart} {string.Join(" ", remainingOwners)}";
        }
    }
}
