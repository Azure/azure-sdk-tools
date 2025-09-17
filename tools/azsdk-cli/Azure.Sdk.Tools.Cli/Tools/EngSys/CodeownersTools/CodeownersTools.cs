using System.ComponentModel;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.RegularExpressions;

using ModelContextProtocol.Server;
using Octokit;

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.CodeownersUtils.Editing;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Commands;

namespace Azure.Sdk.Tools.Cli.Tools.EngSys
{
    [Description("Tool that validates and manipulates codeowners files.")]
    [McpServerToolType]
    public class CodeownersTools : MCPTool
    {
        private readonly IGitHubService githubService;
        private readonly IOutputHelper output;
        private readonly ILogger<CodeownersTools> logger;
        private readonly ICodeownersValidatorHelper codeownersValidator;

        // URL constants
        private const string azureWriteTeamsBlobUrl = "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/azure-sdk-write-teams-blob";

        // Command names
        private const string updateCodeownersCommandName = "update";
        private const string validateCodeownersEntryCommandName = "validate";

        // Core command options
        private readonly Option<string> repoOption = new(["--repo", "-r"], "The repository name") { IsRequired = true };
        private readonly Option<bool> isMgmtPlaneOption = new(["--mgmt-plane"], "Indicates whether this service is a management-plane library") { IsRequired = true };
        private readonly Option<string> pathOptionOptional = new(["--path", "-p"], "The repository path to check/validate") { IsRequired = false };
        private readonly Option<string> serviceLabelOption = new(["--service-label"], "The service label") { IsRequired = false };
        private readonly Option<string[]> serviceOwnersOption = new(["--service-owners"], "The service owners (space-separated)") { IsRequired = false };
        private readonly Option<string[]> sourceOwnersOption = new(["--source-owners"], "The source owners (space-separated)") { IsRequired = false };
        private readonly Option<bool> isAddingOption = new(["--is-adding"], "Whether to add (true) or remove (false) owners") { IsRequired = false };
        private readonly Option<int> prNumberOption = new(["--pr-number"], "PR number.") { IsRequired = false };

        public CodeownersTools(
            IGitHubService githubService,
            IOutputHelper output,
            ILogger<CodeownersTools> logger,
            ILoggerFactory? loggerFactory,
            ICodeownersValidatorHelper codeownersValidator) : base()
        {
            this.githubService = githubService;
            this.output = output;
            this.logger = logger;
            this.codeownersValidator = codeownersValidator;

            CodeownersUtils.Utils.Log.Configure(loggerFactory);

            CommandHierarchy =
            [
                SharedCommandGroups.EngSys
            ];
        }

        public override Command GetCommand()
        {
            var command = new Command("codeowners", "A tool to validate and modify codeowners.");
            var subCommands = new[]
            {
                new Command(updateCodeownersCommandName, "Update codeowners in a repository")
                {
                    repoOption,
                    isMgmtPlaneOption,
                    pathOptionOptional,
                    serviceLabelOption,
                    serviceOwnersOption,
                    sourceOwnersOption,
                    isAddingOption,
                    prNumberOption,
                },
                new Command(validateCodeownersEntryCommandName, "Validate codeowners for an existing service entry")
                {
                    repoOption,
                    serviceLabelOption,
                    pathOptionOptional,
                    prNumberOption,
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

            if (command == updateCodeownersCommandName)
            {
                var repoValue = commandParser.GetValueForOption(repoOption);
                var isMgmtPlaneValue = commandParser.GetValueForOption(isMgmtPlaneOption);
                var pathValue = commandParser.GetValueForOption(pathOptionOptional);
                var serviceLabelValue = commandParser.GetValueForOption(serviceLabelOption);
                var serviceOwnersValue = commandParser.GetValueForOption(serviceOwnersOption);
                var sourceOwnersValue = commandParser.GetValueForOption(sourceOwnersOption);
                var isAddingValue = commandParser.GetValueForOption(isAddingOption);
                var prNumberValue = commandParser.GetValueForOption(prNumberOption);

                var addResult = await UpdateCodeowners(
                    repoValue ?? "",
                    isMgmtPlaneValue,
                    pathValue ?? "",
                    serviceLabelValue ?? "",
                    serviceOwnersValue?.ToList() ?? new List<string>(),
                    sourceOwnersValue?.ToList() ?? new List<string>(),
                    isAddingValue,
                    prNumberValue);
                ctx.ExitCode = ExitCode;
                output.Output(addResult);
                return;
            }
            else if (command == validateCodeownersEntryCommandName)
            {
                var validateRepo = commandParser.GetValueForOption(repoOption);
                var validateServiceLabel = commandParser.GetValueForOption(serviceLabelOption);
                var validateRepoPath = commandParser.GetValueForOption(pathOptionOptional);
                var prNumberValue = commandParser.GetValueForOption(prNumberOption);

                var validateResult = await ValidateCodeownersEntryForService(
                    validateRepo ?? "",
                    validateServiceLabel,
                    validateRepoPath,
                    prNumberValue);
                ctx.ExitCode = ExitCode;
                output.Output(validateResult);
                return;
            }
            else
            {
                SetFailure();
                output.OutputError($"Unknown command: '{command}'");
                return;
            }
        }

        [McpServerTool(Name = "azsdk_engsys_codeowner_update"), Description("Adds or deletes codeowners for a given service label or path in a repo. When isAdding is false, the inputted users will be removed.")]
        public async Task<string> UpdateCodeowners(
            string repo,
            bool isMgmtPlane,
            string path = "",
            string serviceLabel = "",
            List<string> serviceOwners = null,
            List<string> sourceOwners = null,
            bool isAdding = false,
            int prNumber = 0)
        {
            try
            {
                // Validate atleast Service Label or Path.
                if (string.IsNullOrWhiteSpace(serviceLabel) && string.IsNullOrWhiteSpace(path))
                {
                    throw new Exception($"Service label: {serviceLabel} and Path: {path} are both invalid. At least one must be valid");
                }

                string workingBranch = "";
                string repoOwner = "";

                // Resolve PR number to actual branch name if provided.
                if (prNumber > 0)
                {
                    var pr = await githubService.GetPullRequestAsync(Constants.AZURE_OWNER_PATH, repo, prNumber);
                    if (pr == null)
                    {
                        throw new Exception($"Pull request #{prNumber} could not be found or retrieved from repository '{repo}'.");
                    }
                    workingBranch = pr.Head.Ref;
                    repoOwner = pr.Head.Repository.Owner.Login;
                }

                if (string.IsNullOrEmpty(repoOwner))
                {
                    repoOwner = Constants.AZURE_OWNER_PATH;
                }

                // Get codeowners file contents.
                var codeownersFileContent = await githubService.GetContentsSingleAsync(repoOwner, repo, Constants.AZURE_CODEOWNERS_PATH, workingBranch);

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
                    workingBranch,
                    repoOwner);

                return string.Join("\n", resultMessages.Concat(new[] { codeownersValidationResultMessage }));
            }
            catch (Exception ex)
            {
                SetFailure();
                logger.LogError($"Error: {ex}");
                return $"Error: {ex.Message}";
            }
        }

        private async Task<List<string>> CreateCodeownersPR(
            string repo,
            string modifiedContent,
            string description, // used for commit message, PR title, and PR description
            string branchPrefix,
            string identifier,
            string workingBranch,
            string repoOwner)
        {
            List<string> resultMessages = new();
            var branchName = "";
            var hasPushPermissions = await githubService.HasPushPermission(repoOwner, repo);

            if (!hasPushPermissions)
            {
                resultMessages.Add($"GitHub token does not have permission to push to {repoOwner} repository, opening a new PR on a branch in the main repo");
                repoOwner = Constants.AZURE_OWNER_PATH;
                workingBranch = "";
            }

            // Check if we have a working branch from SDK generation
            if (!string.IsNullOrEmpty(workingBranch) && await githubService.IsExistingBranchAsync(repoOwner, repo, workingBranch))
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
            var codeownersFileContent = await githubService.GetContentsSingleAsync(repoOwner, repo, Constants.AZURE_CODEOWNERS_PATH, branchName);

            if (codeownersFileContent == null)
            {
                throw new Exception("Could not retrieve CODEOWNERS file from the repository.");
            }

            var codeownersSha = codeownersFileContent.Sha;

            // Use codeownersSha in UpdateFileAsync
            await githubService.UpdateFileAsync(repoOwner, repo, Constants.AZURE_CODEOWNERS_PATH, description, modifiedContent, codeownersSha, branchName);

            var prInfoList = await githubService.CreatePullRequestAsync(repo, Constants.AZURE_OWNER_PATH, "main", branchName, "[CODEOWNERS] " + description, description);
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

        [McpServerTool(Name = "azsdk_engsys_validate_codeowners_entry_for_service"), Description("Validates codeowners in a specific repository for a given service or repo path.")]
        public async Task<ServiceCodeownersResult> ValidateCodeownersEntryForService(string repoName, string? serviceLabel = null, string? path = null, int prNumber = 0)
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

                string workingBranch = "";
                string repoOwner = Constants.AZURE_OWNER_PATH;

                if (prNumber > 0)
                {
                    var pr = await githubService.GetPullRequestAsync(Constants.AZURE_OWNER_PATH, repoName, prNumber);
                    if (pr == null)
                    {
                        response.Message += $"Pull request #{prNumber} not found in repository '{repoName}'.";
                        return response;
                    }
                    workingBranch = pr.Head.Ref;
                    repoOwner = pr.Head.Repository.Owner.Login;
                }

                if (workingBranch.Equals("main", StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception($"Cannot make changes on branch: {workingBranch}");
                }

                CodeownersEntry? matchingEntry;
                try
                {
                    var contents = await githubService.GetContentsSingleAsync(repoOwner, repoName, ".github/CODEOWNERS", workingBranch);
                    if (contents == null)
                    {
                        throw new Exception($"Could not retrieve upstream CODEOWNERS ({repoName}) for the requested branch.");
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
                var result = await codeownersValidator.ValidateCodeOwnerAsync(username, verbose: false);

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
    }
}
