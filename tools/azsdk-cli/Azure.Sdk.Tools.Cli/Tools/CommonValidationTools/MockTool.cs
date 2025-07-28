using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Models;
using ModelContextProtocol.Server;
using System.CommandLine;
using Azure.Sdk.Tools.Cli.Contract;
using System.CommandLine.Invocation;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using System.Collections.Concurrent;

namespace Azure.Sdk.Tools.Cli.Tools
{
    /// <summary>
    /// A barebones MCP tool for demonstration purposes.
    /// </summary>
    [Description("A mock tool that demonstrates the basic MCP tool structure")]
    [McpServerToolType]
    public class MockTool(
        IGitHubService githubService,
        IOutputService output,
        ILogger<MockTool> logger,
        ICodeOwnerHelper codeownerHelper,
        ICodeOwnerValidator codeOwnerValidator) : MCPTool
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
        private const string addCodeownersCommandName = "add-codeowners";

        // Core command options
        private readonly Option<string> repoOption = new(["--repo", "-r"], "The repository name") { IsRequired = true };
        private readonly Option<string> pathOption = new(["--path", "-p"], "The path for the codeowners entry") { IsRequired = true };
        private readonly Option<string> serviceLabelOption = new(["--service-label", "-sl"], "The service label");
        private readonly Option<string[]> serviceOwnersOption = new(["--service-owners", "-so"], "The service owners (space-separated)");
        private readonly Option<string[]> sourceOwnersOption = new(["--source-owners", "-sro"], "The source owners (space-separated)") { IsRequired = true };

        public override Command GetCommand()
        {
            var command = new Command("mock-tool", "A barebones mock tool for demonstration.");
            var subCommands = new[]
            {
                new Command(addCodeownersCommandName, "Add codeowners to a repository")
                {
                    repoOption,
                    pathOption,
                    serviceLabelOption,
                    serviceOwnersOption,
                    sourceOwnersOption
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
                case addCodeownersCommandName:
                    var repoValue = commandParser.GetValueForOption(repoOption);
                    var pathValue = commandParser.GetValueForOption(pathOption);
                    var serviceLabelValue = commandParser.GetValueForOption(serviceLabelOption);
                    var serviceOwnersValue = commandParser.GetValueForOption(serviceOwnersOption);
                    var sourceOwnersValue = commandParser.GetValueForOption(sourceOwnersOption);

                    var addResult = await AddCodeownerEntry(
                        repoValue ?? "",
                        pathValue ?? "",
                        serviceLabelValue ?? "",
                        serviceOwnersValue?.ToList() ?? new List<string>(),
                        sourceOwnersValue?.ToList() ?? new List<string>());

                    output.Output(addResult);
                    return;
                default:
                    SetFailure();
                    output.OutputError($"Unknown command: '{command}'");
                    return;
            }
        }

        [McpServerTool(Name = "ValidateCodeOwnersForService"), Description("Validates code owners in a specific repository for a given service or repo path.")]
        public async Task<ServiceCodeOwnerResult> ValidateCodeOwners(string repoName, string serviceLabel = null, string repoPath = null)
        {
            ServiceCodeOwnerResult response = new() { };

            try
            {
                if (string.IsNullOrEmpty(serviceLabel) && string.IsNullOrEmpty(repoPath))
                {
                    response.Message += "Must provide a service label or a repository path.";
                    return response;
                }

                azureRepositories.TryGetValue(repoName, out var repoInfo);
                var fullRepoName = repoInfo.RepoName;

                var codeownersUrl = $"https://raw.githubusercontent.com/Azure/{fullRepoName}/main/.github/CODEOWNERS";
                var codeownersEntries = CodeownersParser.ParseCodeownersFile(codeownersUrl, "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/azure-sdk-write-teams-blob");
                var matchingEntries = codeownerHelper.FindMatchingEntries(codeownersEntries, serviceLabel, repoPath);

                if (matchingEntries != null && matchingEntries.Count > 0)
                {
                    var uniqueOwners = new List<string>();
                    foreach (var matchingEntry in matchingEntries)
                    {
                        uniqueOwners.AddRange(codeownerHelper.ExtractUniqueOwners(matchingEntry));
                    }

                    var codeOwners = await ValidateCodeOwnersConcurrently(uniqueOwners);
                    response.CodeOwners = codeOwners;
                    response.Message += "Successfully found codeowners.";
                    response.Repository = fullRepoName;
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

        [McpServerTool(Name = "isValidCodeOwner"), Description("Validates if the user is a code owner given their GitHub alias. (Default is the current user)")]
        public async Task<string> isValidCodeOwner(string githubAlias = "")
        {
            try
            {
                // Get the current user's GitHub username if not provided
                var user = await githubService.GetGitUserDetailsAsync();
                var userDetails = string.IsNullOrEmpty(githubAlias) ? user?.Login : githubAlias;

                if (string.IsNullOrEmpty(userDetails))
                {
                    var errorResponse = new GenericResponse()
                    {
                        Status = "Failed",
                        Details = { "Unable to determine GitHub username" }
                    };
                    return output.Format(errorResponse);
                }

                var validationResult = await codeOwnerValidator.ValidateCodeOwnerAsync(userDetails, verbose: false);

                // Convert to the expected JSON format
                var result = new
                {
                    validationResult.Organizations,
                    validationResult.HasWritePermission,
                    validationResult.IsValidCodeOwner
                };

                return System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                var errorResponse = new GenericResponse()
                {
                    Status = "Failed",
                    Details = { $"Failed to validate GitHub code owner. Error: {ex.Message}" }
                };
                return output.Format(errorResponse);
            }
        }

        [McpServerTool(Name = "AddCodeownerEntry"), Description("Adds a codeowner entry for a given service label or path for a repo.")]
        public async Task<string> AddCodeownerEntry(
            string repo,
            string path,
            string serviceLabel,
            List<string> serviceOwners,
            List<string> sourceOwners)
        {
            try
            {
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

                var (startLine, endLine) = codeownerHelper.findBlock(content, serviceCategory);

                var codeownersEntries = CodeownersParser.ParseCodeownersFile(codeownersUrl, "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/azure-sdk-write-teams-blob", startLine, endLine);
                codeownersEntries = codeownerHelper.mergeSimilarCodeownersEntries(codeownersEntries);

                var insertionIndex = codeownerHelper.findAlphabeticalInsertionPoint(codeownersEntries, path, serviceLabel);

                var formattedCodeownersEntry = codeownerHelper.formatCodeownersEntry(path, serviceLabel, serviceOwners, sourceOwners);
                var modifiedCodeownersContent = codeownerHelper.addCodeownersEntryAtIndex(content, formattedCodeownersEntry, insertionIndex);

                string result = "";

                var branchName = codeownerHelper.CreateBranchName("add-codeowner-entry", path ?? serviceLabel);
                var createBranchInfo = await githubService.CreateBranchAsync("Azure", fullRepoName, branchName);
                var updateFileInfo = await githubService.UpdateFileAsync("Azure", fullRepoName, ".github/CODEOWNERS", $"Add codeowner entry for {path ?? serviceLabel}", modifiedCodeownersContent, sha, branchName);
                var PRInfo = await githubService.CreatePullRequestAsync(fullRepoName, "Azure", "main", branchName, $"Add codeowner entry for {path ?? serviceLabel}", $"Add codeowner entry for {path ?? serviceLabel}", true);

                result = string.Join("\n", createBranchInfo, updateFileInfo, PRInfo);

                return result;
            }
            catch (Exception ex)
            {
                logger.LogInformation($"{ex}");
                return $"Error: {ex}";
            }
        }

        [McpServerTool(Name = "AddCodeowners"), Description("Adds codeowners to a given service label or path for a repo.")]
        public async Task<List<CodeownersEntry>> AddCodeowners(
            string repo,
            string path,
            string serviceLabel,
            List<string> serviceOwners,
            List<string> sourceOwners)
        {
            try
            {
                var codeownersUrl = $"https://raw.githubusercontent.com/Azure/{repo}/main/.github/CODEOWNERS";
                var codeownersEntries = CodeownersParser.ParseCodeownersFile(codeownersUrl, "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/azure-sdk-write-teams-blob");

                return codeownersEntries;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in AddCodeowners");
                return new List<CodeownersEntry>();
            }
        }
        
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
