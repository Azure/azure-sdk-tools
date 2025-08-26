using System.ComponentModel;
using System.CommandLine;
using System.CommandLine.Invocation;

using ModelContextProtocol.Server;
using Octokit;

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
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
        private readonly ICodeownersHelper codeownersHelper;
        private readonly ICodeownersValidatorHelper codeownersValidator;

        // URL constants
        private const string azureWriteTeamsBlobUrl = "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/azure-sdk-write-teams-blob";

        // Command names
        private const string updateCodeownersCommandName = "update";
        private const string validateCodeownersEntryCommandName = "validate";
        private const string publicizeOrgMembershipCommandName = "azsdk_publicize_org_membership";

        // Core command options
        private readonly Option<string> repoOption = new(["--repo", "-r"], "The repository name") { IsRequired = true };
        private readonly Option<bool> isMgmtPlaneOption = new(["--mgmt-plane"], "Indicates whether this service is a management-plane library") { IsRequired = true };
        private readonly Option<string> pathOptionOptional = new(["--path", "-p"], "The repository path to check/validate") { IsRequired = false };
        private readonly Option<string> serviceLabelOption = new(["--service-label"], "The service label") { IsRequired = false };
        private readonly Option<string[]> serviceOwnersOption = new(["--service-owners"], "The service owners (space-separated)") { IsRequired = false };
        private readonly Option<string[]> sourceOwnersOption = new(["--source-owners"], "The source owners (space-separated)") { IsRequired = false };
        private readonly Option<bool> isAddingOption = new(["--is-adding"], "Whether to add (true) or remove (false) owners") { IsRequired = false };
        private readonly Option<string> workingBranchOption = new(["--branch"], "Branch to make edits to, only if provided.") { IsRequired = false };
        private readonly Option<string> organizationOption = new(["--organization", "-o"], "The GitHub organization name") { IsRequired = true };
        private readonly Option<string> usernameOption = new(["--username", "-u"], "The GitHub username (defaults to current authenticated user)") { IsRequired = false };

        public CodeownersTools(
            IGitHubService githubService,
            IOutputHelper output,
            ILogger<CodeownersTools> logger,
            ICodeownersHelper codeownersHelper,
            ICodeownersValidatorHelper codeownersValidator) : base()
        {
            this.githubService = githubService;
            this.output = output;
            this.logger = logger;
            this.codeownersHelper = codeownersHelper;
            this.codeownersValidator = codeownersValidator;

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
                    workingBranchOption,
                },
                new Command(validateCodeownersEntryCommandName, "Validate codeowners for an existing service entry")
                {
                    repoOption,
                    serviceLabelOption,
                    pathOptionOptional
                },
                new Command(publicizeOrgMembershipCommandName, "Make GitHub organization membership public")
                {
                    organizationOption,
                    usernameOption
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
                var workingBranchValue = commandParser.GetValueForOption(workingBranchOption);

                var addResult = await UpdateCodeowners(
                    repoValue ?? "",
                    isMgmtPlaneValue,
                    pathValue ?? "",
                    serviceLabelValue ?? "",
                    serviceOwnersValue?.ToList() ?? new List<string>(),
                    sourceOwnersValue?.ToList() ?? new List<string>(),
                    isAddingValue,
                    workingBranchValue ?? "");
                ctx.ExitCode = ExitCode;
                output.Output(addResult);
                return;
            }
            else if (command == validateCodeownersEntryCommandName)
            {
                var validateRepo = commandParser.GetValueForOption(repoOption);
                var validateServiceLabel = commandParser.GetValueForOption(serviceLabelOption);
                var validateRepoPath = commandParser.GetValueForOption(pathOptionOptional);

                var validateResult = await ValidateCodeownersEntryForService(
                    validateRepo ?? "",
                    validateServiceLabel,
                    validateRepoPath);
                ctx.ExitCode = ExitCode;
                output.Output(validateResult);
                return;
            }
            else if (command == publicizeOrgMembershipCommandName)
            {
                var organization = commandParser.GetValueForOption(organizationOption);
                var username = commandParser.GetValueForOption(usernameOption);

                var result = await PublicizeOrgMembership(
                    organization ?? "",
                    username ?? "");
                output.Output(result);
                ctx.ExitCode = ExitCode;
                return;
            }
            else
            {
                SetFailure();
                output.OutputError($"Unknown command: '{command}'");
                return;
            }
        }

        [McpServerTool(Name = "azsdk_engsys_codeowner_update"), Description("Adds or deletes codeowners for a given service label or path in a repo.")]
        public async Task<string> UpdateCodeowners(
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

                // Normalize service path
                var normalizedPath = CodeownersHelper.NormalizePath(path);

                // Validate service label (perform early so tests expecting exceptions aren't swallowed)
                if (!string.IsNullOrEmpty(serviceLabel))
                {
                    var labelsFileContent = await githubService.GetContentsSingleAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, Constants.AZURE_COMMON_LABELS_PATH);
                    if (labelsFileContent == null)
                    {
                        throw new Exception("Could not retrieve labels file from the repository.");
                    }

                    var labelsContent = labelsFileContent.Content;
                    var serviceLabelValidationResults = LabelHelper.CheckServiceLabel(labelsContent, serviceLabel);
                    if (serviceLabelValidationResults != LabelHelper.ServiceLabelStatus.Exists)
                    {
                        var labelsPullRequests = (await githubService.SearchPullRequestsByTitleAsync(Constants.AZURE_OWNER_PATH, Constants.AZURE_SDK_TOOLS_PATH, "Service Label"))
                            ?? new List<PullRequest?>().AsReadOnly();

                        if (!LabelHelper.CheckServiceLabelInReview(labelsPullRequests, serviceLabel) && string.IsNullOrEmpty(normalizedPath))
                        {
                            throw new Exception($"Service label: {serviceLabel} doesn't exist.");
                        }
                    }
                }

                if (string.IsNullOrEmpty(workingBranch))
                {
                    var codeownersPullRequests = (await githubService.SearchPullRequestsByTitleAsync(Constants.AZURE_OWNER_PATH, repo, "[CODEOWNERS]"))
                        ?? new List<PullRequest?>().AsReadOnly();

                    foreach (var codeownersPullRequest in codeownersPullRequests)
                    {
                        if (codeownersPullRequest != null &&
                            ((!string.IsNullOrEmpty(serviceLabel) && codeownersPullRequest.Title.Contains(serviceLabel, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrEmpty(normalizedPath) && codeownersPullRequest.Title.Contains(normalizedPath, StringComparison.OrdinalIgnoreCase))))
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
                var codeownersContentList = codeownersContent.Split("\n").ToList();

                var (modifiedCodeownersContent, updatedEntry) = codeownersHelper.AddCodeownersEntry(codeownersContent, isMgmtPlane, normalizedPath, serviceLabel, serviceOwners, sourceOwners, isAdding);

                // Validate the modified/created Entry
                var (validationErrors, codeownersValidationResults) = await ValidateMinimumOwnerRequirements(updatedEntry);

                var codeownersValidationResultMessage = string.Join("\n", codeownersValidationResults.Select(r => r.ToString()));
                if (validationErrors.Any())
                {
                    throw new Exception($"{validationErrors} Validation results: {codeownersValidationResultMessage}");
                }

                // Create Branch, Update File, and Handle PR.
                var identifier = !string.IsNullOrWhiteSpace(updatedEntry.ServiceLabels?.FirstOrDefault())
                    ? updatedEntry.ServiceLabels.FirstOrDefault()
                    : updatedEntry.PathExpression;
                var resultMessages = await CreateCodeownersPR(
                    repo,                                                             // Repository name
                    modifiedCodeownersContent,                     // Modified content
                    $"Update codeowners entry for {identifier}", // Description for commit message, PR title, and description
                    "update-codeowners-entry",                                             // Branch prefix for the action
                    identifier, // Identifier for the PR
                    workingBranch);

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
                branchName = CodeownersHelper.CreateBranchName(branchPrefix, identifier);
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

        [McpServerTool(Name = "azsdk_engsys_validate_codeowners_entry_for_service"), Description("Validates codeowners in a specific repository for a given service or repo path.")]
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

                var normalizedPath = CodeownersHelper.NormalizePath(path);

                var workingBranch = "";
                var codeownersPullRequests = await githubService.SearchPullRequestsByTitleAsync(Constants.AZURE_OWNER_PATH, repoName, "[CODEOWNERS]");

                foreach (var codeownersPullRequest in codeownersPullRequests)
                {
                    if (codeownersPullRequest != null &&
                        ((!string.IsNullOrEmpty(serviceLabel) && codeownersPullRequest.Title.Contains(serviceLabel, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(normalizedPath) && codeownersPullRequest.Title.Contains(normalizedPath, StringComparison.OrdinalIgnoreCase))))
                    {
                        workingBranch = codeownersPullRequest.Head.Ref;
                    }
                }

                workingBranch = string.IsNullOrEmpty(workingBranch) ? "main" : workingBranch;

                CodeownersEntry? matchingEntry;
                try
                {
                    var contents = await githubService.GetContentsSingleAsync("Azure", "azure-sdk-for-net", ".github/CODEOWNERS", workingBranch);
                    if (contents == null)
                    {
                        throw new Exception("Could not retrieve upstream CODEOWNERS (azure-sdk-for-net) for the requested branch.");
                    }
                    var codeownersContent = contents.Content;
                    var codeownersSha = contents.Sha;
                    var codeownersContentList = codeownersContent.Split("\n").ToList();

                    var codeownersEntries = CodeownersParser.ParseCodeownersEntries(codeownersContentList, azureWriteTeamsBlobUrl);

                    matchingEntry = CodeownersHelper.FindMatchingEntries(codeownersEntries, path, serviceLabel);
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

        [McpServerTool(Name = "azsdk_publicize_org_membership"), Description("Makes a user's GitHub organization membership public.")]
        public async Task<string> PublicizeOrgMembership(
            string organization,
            string username = "")
        {
            try
            {
                // If no username provided, use the authenticated user
                if (string.IsNullOrEmpty(username))
                {
                    var currentUser = await githubService.GetCurrentUserAsync();
                    if (currentUser == null)
                    {
                        throw new Exception("Could not retrieve current authenticated user. Ensure you're logged into GitHub.");
                    }
                    username = currentUser.Login;
                }

                // Validate that the user is a member of the organization first
                var isMember = await githubService.IsUserMemberOfOrgAsync(organization, username);
                if (!isMember)
                {
                    throw new Exception($"User '{username}' is not a member of organization '{organization}' or membership cannot be verified. User must be added to the organization first.");
                }

                // Make membership public
                var publicResult = await githubService.MakeOrgMembershipPublicAsync(organization, username);

                if (publicResult)
                {
                    return $"Successfully made {username}'s membership in '{organization}' public.";
                }
                else
                {
                    throw new Exception($"Failed to make membership public for {username} in organization '{organization}'.");
                }
            }
            catch (Exception ex)
            {
                SetFailure();
                logger.LogError($"Error publicizing GitHub organization membership: {ex}");
                return $"Error: {ex.Message}";
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
    }
}
