// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.AI.OpenAI;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using ModelContextProtocol.Server;
using OpenAI.Chat;

namespace Azure.Sdk.Tools.Cli.Tools;

[McpServerToolType, Description("Example tool demonstrating various framework features and service integrations")]
public class ExampleTool : MCPTool
{
    // Sub-command constants
    private const string AzureSubCommand = "azure";
    private const string DevOpsSubCommand = "devops";
    private const string GitHubSubCommand = "github";
    private const string AISubCommand = "ai";
    private const string ErrorSubCommand = "error";

    // Dependencies injected via constructor
    private readonly ILogger<ExampleTool> logger;
    private readonly IOutputService output;
    private readonly IAzureService azureService;
    private readonly IDevOpsService devOpsService;
    private readonly IGitHubService gitHubService;
    private readonly IAzureAgentServiceFactory agentServiceFactory;
    private readonly AzureOpenAIClient openAIClient;

    // CLI Options and Arguments
    private readonly Argument<string> inputArg = new Argument<string>(
        name: "input",
        description: "Input parameter for demonstration (e.g., repository name, work item ID, etc.)"
    ) { Arity = ArgumentArity.ExactlyOne };

    private readonly Option<string> repoOption = new(["--repo", "-r"], "Repository name (owner/repo format)");
    private readonly Option<int> itemIdOption = new(["--item-id", "-i"], "Item ID (work item, issue, PR number)");
    private readonly Option<string> promptOption = new(["--prompt", "-p"], "AI prompt text");
    private readonly Option<bool> forceFailureOption = new(["--force-failure", "-f"], () => false, "Force an error for demonstration");
    private readonly Option<bool> verboseOption = new(["--verbose", "-v"], () => false, "Enable verbose logging");

    public ExampleTool(
        ILogger<ExampleTool> logger,
        IOutputService output,
        IAzureService azureService,
        IDevOpsService devOpsService,
        IGitHubService gitHubService,
        IAzureAgentServiceFactory agentServiceFactory,
        AzureOpenAIClient openAIClient
    ) : base()
    {
        this.logger = logger;
        this.output = output;
        this.azureService = azureService;
        this.devOpsService = devOpsService;
        this.gitHubService = gitHubService;
        this.agentServiceFactory = agentServiceFactory;
        this.openAIClient = openAIClient;

        // Set command hierarchy - results in: azsdk example
        CommandHierarchy = [
            SharedCommandGroups.Example
        ];
    }

    public override Command GetCommand()
    {
        var parentCommand = new Command("demo", "Comprehensive demonstration of framework features");

        // Azure service example sub-command
        var azureCmd = new Command(AzureSubCommand, "Demonstrate Azure service integration");
        azureCmd.AddArgument(inputArg);
        azureCmd.AddOption(verboseOption);
        azureCmd.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });

        // DevOps service example sub-command
        var devopsCmd = new Command(DevOpsSubCommand, "Demonstrate DevOps service integration");
        devopsCmd.AddArgument(inputArg);
        devopsCmd.AddOption(itemIdOption);
        devopsCmd.AddOption(verboseOption);
        devopsCmd.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });

        // GitHub service example sub-command
        var githubCmd = new Command(GitHubSubCommand, "Demonstrate GitHub service integration");
        githubCmd.AddArgument(inputArg);
        githubCmd.AddOption(repoOption);
        githubCmd.AddOption(itemIdOption);
        githubCmd.AddOption(verboseOption);
        githubCmd.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });

        // AI service example sub-command
        var aiCmd = new Command(AISubCommand, "Demonstrate AI service integration");
        aiCmd.AddArgument(inputArg);
        aiCmd.AddOption(promptOption);
        aiCmd.AddOption(verboseOption);
        aiCmd.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });

        // Error handling example sub-command
        var errorCmd = new Command(ErrorSubCommand, "Demonstrate error handling patterns");
        errorCmd.AddArgument(inputArg);
        errorCmd.AddOption(forceFailureOption);
        errorCmd.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });

        parentCommand.Add(azureCmd);
        parentCommand.Add(devopsCmd);
        parentCommand.Add(githubCmd);
        parentCommand.Add(aiCmd);
        parentCommand.Add(errorCmd);

        return parentCommand;
    }

    public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        var commandName = ctx.ParseResult.CommandResult.Command.Name;
        var input = ctx.ParseResult.GetValueForArgument(inputArg);
        var verbose = ctx.ParseResult.GetValueForOption(verboseOption);

        try
        {
            object result = commandName switch
            {
                AzureSubCommand => await DemonstrateAzureService(input, verbose, ct),
                DevOpsSubCommand => await DemonstrateDevOpsService(input, ctx.ParseResult.GetValueForOption(itemIdOption), verbose, ct),
                GitHubSubCommand => await DemonstrateGitHubService(input, ctx.ParseResult.GetValueForOption(repoOption), ctx.ParseResult.GetValueForOption(itemIdOption), verbose, ct),
                AISubCommand => await DemonstrateAIService(input, ctx.ParseResult.GetValueForOption(promptOption), verbose, ct),
                ErrorSubCommand => await DemonstrateErrorHandling(input, ctx.ParseResult.GetValueForOption(forceFailureOption), ct),
                _ => throw new InvalidOperationException($"Unknown command: {commandName}")
            };

            ctx.ExitCode = ExitCode;
            output.Output(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing example command {Command} with input {Input}", commandName, input);
            ctx.ExitCode = 1;
            output.Output($"Error: {ex.Message}");
        }
    }

    [McpServerTool(Name = "example_azure_service"), Description("Demonstrates Azure service integration")]
    public async Task<ExampleServiceResponse> DemonstrateAzureService(string tenantInfo = "default", bool verbose = false, CancellationToken ct = default)
    {
        try
        {
            if (verbose) logger.LogInformation("Starting Azure service demonstration with input: {TenantInfo}", tenantInfo);

            // Demonstrate Azure service usage
            var credential = azureService.GetCredential();

            if (verbose) logger.LogInformation("Successfully obtained Azure credentials");

            // Get token for demonstration (but don't log the actual token)
            var tokenResult = await credential.GetTokenAsync(
                new Azure.Core.TokenRequestContext(new[] { "https://management.azure.com/.default" }),
                ct);

            var details = new Dictionary<string, string>
            {
                ["credential_type"] = credential.GetType().Name,
                ["token_expires"] = tokenResult.ExpiresOn.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                ["has_token"] = (!string.IsNullOrEmpty(tokenResult.Token)).ToString()
            };

            if (verbose) logger.LogInformation("Azure credential details retrieved successfully");

            return new ExampleServiceResponse
            {
                ServiceName = "Azure Authentication",
                Operation = "GetCredential",
                Result = "Successfully obtained Azure credentials and token",
                Details = details
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error demonstrating Azure service with input: {TenantInfo}", tenantInfo);
            SetFailure();
            return new ExampleServiceResponse
            {
                ResponseError = $"Failed to demonstrate Azure service: {ex.Message}"
            };
        }
    }

    [McpServerTool(Name = "example_devops_service"), Description("Demonstrates DevOps service integration")]
    public async Task<ExampleServiceResponse> DemonstrateDevOpsService(string projectInfo, int? workItemId = null, bool verbose = false, CancellationToken ct = default)
    {
        try
        {
            if (verbose) logger.LogInformation("Starting DevOps service demonstration with project: {ProjectInfo}, workItem: {WorkItemId}", projectInfo, workItemId);

            var details = new Dictionary<string, string>
            {
                ["project_info"] = projectInfo,
                ["service_type"] = "Azure DevOps",
                ["demo_mode"] = "true"
            };

            // Note: In a real scenario, you might call devOpsService methods like:
            // var releasePlan = await devOpsService.GetReleasePlanAsync(workItemId.Value);
            // But for demonstration, we'll simulate the response

            if (workItemId.HasValue)
            {
                details["work_item_id"] = workItemId.Value.ToString();
                details["simulated_operation"] = "GetReleasePlan";
                if (verbose) logger.LogInformation("Simulating work item retrieval for ID: {WorkItemId}", workItemId.Value);
            }
            else
            {
                details["simulated_operation"] = "ListProjects";
                if (verbose) logger.LogInformation("Simulating project information retrieval");
            }

            // Add a small delay to simulate async work
            await Task.Delay(50, ct);

            return new ExampleServiceResponse
            {
                ServiceName = "Azure DevOps",
                Operation = details["simulated_operation"],
                Result = $"Successfully demonstrated DevOps service integration for project: {projectInfo}",
                Details = details
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error demonstrating DevOps service with project: {ProjectInfo}, workItem: {WorkItemId}", projectInfo, workItemId);
            SetFailure();
            return new ExampleServiceResponse
            {
                ResponseError = $"Failed to demonstrate DevOps service: {ex.Message}"
            };
        }
    }

    [McpServerTool(Name = "example_github_service"), Description("Demonstrates GitHub service integration")]
    public async Task<ExampleServiceResponse> DemonstrateGitHubService(string operation, string? repository = null, int? itemId = null, bool verbose = false, CancellationToken ct = default)
    {
        try
        {
            if (verbose) logger.LogInformation("Starting GitHub service demonstration with operation: {Operation}, repo: {Repository}, itemId: {ItemId}", operation, repository, itemId);

            var details = new Dictionary<string, string>
            {
                ["operation"] = operation,
                ["service_type"] = "GitHub API",
            };

            string result;

            switch (operation.ToLower())
            {
                case "user":
                    var user = await gitHubService.GetGitUserDetailsAsync();
                    details["user_login"] = user.Login;
                    details["user_id"] = user.Id.ToString();
                    result = $"Retrieved user details: {user.Login} (ID: {user.Id})";
                    break;

                case "pullrequest":
                case "pr":
                    if (string.IsNullOrEmpty(repository) || !itemId.HasValue)
                    {
                        throw new ArgumentException("Repository and item ID are required for pull request operations");
                    }
                    var repoParts = repository.Split('/');
                    if (repoParts.Length != 2)
                    {
                        throw new ArgumentException("Repository must be in format 'owner/repo'");
                    }
                    var pr = await gitHubService.GetPullRequestAsync(repoParts[0], repoParts[1], itemId.Value);
                    details["pr_title"] = pr.Title;
                    details["pr_state"] = pr.State.ToString();
                    details["pr_url"] = pr.HtmlUrl;
                    result = $"Retrieved PR #{itemId.Value}: {pr.Title} ({pr.State})";
                    break;

                case "issue":
                    if (string.IsNullOrEmpty(repository) || !itemId.HasValue)
                    {
                        throw new ArgumentException("Repository and item ID are required for issue operations");
                    }
                    repoParts = repository.Split('/');
                    if (repoParts.Length != 2)
                    {
                        throw new ArgumentException("Repository must be in format 'owner/repo'");
                    }
                    var issue = await gitHubService.GetIssueAsync(repoParts[0], repoParts[1], itemId.Value);
                    details["issue_title"] = issue.Title;
                    details["issue_state"] = issue.State.ToString();
                    result = $"Retrieved Issue #{itemId.Value}: {issue.Title} ({issue.State})";
                    break;

                default:
                    throw new ArgumentException($"Unknown operation: {operation}. Supported: user, pullrequest, issue");
            }

            if (verbose) logger.LogInformation("GitHub service demonstration completed successfully");

            return new ExampleServiceResponse
            {
                ServiceName = "GitHub",
                Operation = operation,
                Result = result,
                Details = details
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error demonstrating GitHub service with operation: {Operation}, repo: {Repository}, itemId: {ItemId}", operation, repository, itemId);
            SetFailure();
            return new ExampleServiceResponse
            {
                ResponseError = $"Failed to demonstrate GitHub service: {ex.Message}"
            };
        }
    }

    [McpServerTool(Name = "example_ai_service"), Description("Demonstrates AI service integration using Azure OpenAI")]
    public async Task<ExampleAIResponse> DemonstrateAIService(string userPrompt, string? customPrompt = null, bool verbose = false, CancellationToken ct = default)
    {
        try
        {
            if (verbose) logger.LogInformation("Starting AI service demonstration with prompt: {UserPrompt}", userPrompt);

            // Use custom prompt or create a simple demonstration prompt
            var prompt = customPrompt ?? $"You are a helpful assistant. Please respond to this user input: {userPrompt}";

            if (verbose) logger.LogInformation("Sending request to Azure OpenAI");

            // Get ChatClient from AzureOpenAIClient
            var chatClient = openAIClient.GetChatClient("gpt-4"); // This would typically come from configuration
            
            var messages = new ChatMessage[]
            {
                new SystemChatMessage("You are a helpful assistant for the Azure SDK CLI tool demonstration."),
                new UserChatMessage(prompt)
            };

            var response = await chatClient.CompleteChatAsync(messages, cancellationToken: ct);
            
            // Token usage information would be available if needed
            var tokenUsage = new Dictionary<string, int>
            {
                ["demo_mode"] = 1, // Placeholder - actual usage would depend on available properties
                ["model_used"] = 1
            };

            if (verbose) logger.LogInformation("AI service demonstration completed successfully");

            return new ExampleAIResponse
            {
                Prompt = userPrompt,
                ResponseText = response.Value.Content[0].Text,
                Model = "gpt-4",
                TokenUsage = tokenUsage
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error demonstrating AI service with prompt: {UserPrompt}", userPrompt);
            SetFailure();
            return new ExampleAIResponse
            {
                ResponseError = $"Failed to demonstrate AI service: {ex.Message}"
            };
        }
    }

    [McpServerTool(Name = "example_error_handling"), Description("Demonstrates error handling patterns in tools")]
    public async Task<DefaultCommandResponse> DemonstrateErrorHandling(string scenario, bool forceFailure = false, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Starting error handling demonstration with scenario: {Scenario}, forceFailure: {ForceFailure}", scenario, forceFailure);

            if (forceFailure)
            {
                // Simulate different types of errors
                switch (scenario.ToLower())
                {
                    case "argument":
                        throw new ArgumentException("Simulated argument validation error");
                    case "timeout":
                        throw new TimeoutException("Simulated timeout error");
                    case "notfound":
                        throw new FileNotFoundException("Simulated resource not found error");
                    default:
                        throw new InvalidOperationException("Simulated generic operation error");
                }
            }

            // Simulate successful operation
            await Task.Delay(100, ct); // Simulate some work

            return new DefaultCommandResponse
            {
                Result = $"Error handling demonstration completed successfully for scenario: {scenario}. " +
                        "This shows how tools handle normal operations and structured responses."
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Demonstrating error handling for scenario: {Scenario}", scenario);
            SetFailure();
            return new DefaultCommandResponse
            {
                ResponseError = $"Demonstrated error handling: {ex.GetType().Name}: {ex.Message}"
            };
        }
    }
}
