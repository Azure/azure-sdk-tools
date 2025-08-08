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
using Azure.Sdk.Tools.Cli.Helpers;
using ModelContextProtocol.Server;
using OpenAI.Chat;

namespace Azure.Sdk.Tools.Cli.Tools;

#if DEBUG
[McpServerToolType, Description("Example tool demonstrating various framework features and service integrations")]
public class ExampleTool : MCPTool
{
    // Sub-command constants
    private const string AzureSubCommand = "azure";
    private const string DevOpsSubCommand = "devops";
    private const string GitHubSubCommand = "github";
    private const string AISubCommand = "ai";
    private const string ErrorSubCommand = "error";
    private const string ProcessSubCommand = "process";

    // Dependencies injected via constructor
    private readonly ILogger<ExampleTool> logger;
    private readonly IOutputService output;
    private readonly IAzureService azureService;
    private readonly IDevOpsService devOpsService;
    private readonly IGitHubService gitHubService;
    private readonly AzureOpenAIClient openAIClient;
    private readonly IProcessHelper processHelper;

    // CLI Options and Arguments
    private readonly Argument<string> aiInputArg = new(
        name: "chat-prompt",
        description: "Chat prompt surrounded with quotes"
    )
    { Arity = ArgumentArity.ExactlyOne };

    private readonly Argument<string> packageArgument = new(
        name: "package",
        description: "Package name"
    )
    { Arity = ArgumentArity.ExactlyOne };

    private readonly Argument<string> errorInputArg = new(
        name: "error-input",
        description: "Error type to simulate, can be argument, timeout, notfound, or any user input"
    )
    { Arity = ArgumentArity.ExactlyOne };

    private readonly Argument<string> processSleepArg = new(
        name: "sleep",
        description: "How many seconds to sleep"
    )
    { Arity = ArgumentArity.ExactlyOne };

    private readonly Option<string> tenantOption = new(["--tenant", "-t"], "Tenant ID");
    private readonly Option<string> languageOption = new(["--language", "-l"], "Programming language of the repository");
    private readonly Option<string> promptOption = new(["--prompt", "-p"], "AI prompt text");
    private readonly Option<bool> forceFailureOption = new(["--force-failure", "-f"], () => false, "Force an error for demonstration");
    private readonly Option<bool> verboseOption = new(["--verbose", "-v"], () => false, "Enable verbose logging");

    public ExampleTool(
        ILogger<ExampleTool> logger,
        IOutputService output,
        IAzureService azureService,
        IDevOpsService devOpsService,
        IGitHubService gitHubService,
        IProcessHelper processHelper,
        AzureOpenAIClient openAIClient
    ) : base()
    {
        this.logger = logger;
        this.output = output;
        this.azureService = azureService;
        this.devOpsService = devOpsService;
        this.gitHubService = gitHubService;
        this.openAIClient = openAIClient;
        this.processHelper = processHelper;

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
        azureCmd.AddOption(tenantOption);
        azureCmd.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });

        // DevOps service example sub-command
        var devopsCmd = new Command(DevOpsSubCommand, "Demonstrate DevOps service integration");
        devopsCmd.AddArgument(packageArgument);
        devopsCmd.AddOption(languageOption);
        devopsCmd.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });

        // GitHub service example sub-command
        var githubCmd = new Command(GitHubSubCommand, "Demonstrate GitHub service integration");
        githubCmd.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });

        // AI service example sub-command
        var aiCmd = new Command(AISubCommand, "Demonstrate AI service integration");
        aiCmd.AddArgument(aiInputArg);
        aiCmd.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });

        // Error handling example sub-command
        var errorCmd = new Command(ErrorSubCommand, "Demonstrate error handling patterns");
        errorCmd.AddArgument(errorInputArg);
        errorCmd.AddOption(forceFailureOption);
        errorCmd.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });

        // Process execution example sub-command
        var processCmd = new Command(ProcessSubCommand, "Demonstrate spawning an external process (echo)");
        processCmd.AddArgument(processSleepArg);
        processCmd.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });

        parentCommand.Add(azureCmd);
        parentCommand.Add(devopsCmd);
        parentCommand.Add(githubCmd);
        parentCommand.Add(aiCmd);
        parentCommand.Add(errorCmd);
        parentCommand.Add(processCmd);

        return parentCommand;
    }

    public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        var commandName = ctx.ParseResult.CommandResult.Command.Name;

        object result = commandName switch
        {
            AzureSubCommand => await DemonstrateAzureService(ctx.ParseResult.GetValueForOption(tenantOption), ct),
            DevOpsSubCommand => await DemonstrateDevOpsService(ctx.ParseResult.GetValueForArgument(packageArgument), ctx.ParseResult.GetValueForOption(languageOption), ct),
            GitHubSubCommand => await DemonstrateGitHubService(ct),
            AISubCommand => await DemonstrateAIService(ctx.ParseResult.GetValueForArgument(aiInputArg), ct),
            ErrorSubCommand => await DemonstrateErrorHandling(ctx.ParseResult.GetValueForArgument(errorInputArg), ctx.ParseResult.GetValueForOption(forceFailureOption), ct),
            ProcessSubCommand => await DemonstrateProcessExecution(ctx.ParseResult.GetValueForArgument(processSleepArg), ct),
            _ => new ExampleServiceResponse { ResponseError = $"Unknown command: {commandName}" }
        };

        ctx.ExitCode = ExitCode;
        output.Output(result);
    }

    [McpServerTool(Name = "azsdk_example_azure_service"), Description("Demonstrates Azure service integration")]
    public async Task<ExampleServiceResponse> DemonstrateAzureService(string? tenantId = null, CancellationToken ct = default)
    {
        try
        {
            var credential = azureService.GetCredential(tenantId);

            // Get token for demonstration (but don't log the actual token)
            var tokenResult = await credential.GetTokenAsync(new Azure.Core.TokenRequestContext(["https://management.azure.com/.default"]), ct);

            var details = new Dictionary<string, string>
            {
                ["credential_type"] = credential.GetType().Name,
                ["token_expires"] = tokenResult.ExpiresOn.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                ["has_token"] = (!string.IsNullOrEmpty(tokenResult.Token)).ToString()
            };

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
            logger.LogError(ex, "Error demonstrating Azure service with input");
            SetFailure();
            return new ExampleServiceResponse
            {
                ResponseError = $"Failed to demonstrate Azure service: {ex.Message}"
            };
        }
    }

    [McpServerTool(Name = "azsdk_example_devops_service"), Description("Demonstrates DevOps service integration")]
    public async Task<ExampleServiceResponse> DemonstrateDevOpsService(string packageName, string language, CancellationToken ct = default)
    {
        try
        {
            var details = new Dictionary<string, string>
            {
                ["service_type"] = "Azure DevOps"
            };

            var pkg = await devOpsService.GetPackageWorkItemAsync(packageName, language);
            details["package_pipeline_url"] = pkg.PipelineDefinitionUrl;

            return new ExampleServiceResponse
            {
                ServiceName = "Azure DevOps",
                Operation = "GetPackagePipelineUrl",
                Result = $"Found package pipeline",
                Details = details
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error demonstrating DevOps service with package: {PackageName}, language: {Language}", packageName, language);
            SetFailure();
            return new ExampleServiceResponse
            {
                ResponseError = $"Failed to demonstrate DevOps service: {ex.Message}"
            };
        }
    }

    [McpServerTool(Name = "azsdk_example_github_service"), Description("Demonstrates GitHub service integration")]
    public async Task<ExampleServiceResponse> DemonstrateGitHubService(CancellationToken ct = default)
    {
        try
        {
            Dictionary<string, string> details = new()
            {
                ["service_type"] = "GitHub API",
            };

            var user = await gitHubService.GetGitUserDetailsAsync();
            details["user_login"] = user.Login;
            details["user_id"] = user.Id.ToString();
            var result = $"Retrieved user details: {user.Login} (ID: {user.Id})";

            return new ExampleServiceResponse
            {
                ServiceName = "GitHub",
                Operation = "GetUser",
                Result = result,
                Details = details
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error demonstrating GitHub service");
            SetFailure();
            return new ExampleServiceResponse
            {
                ResponseError = $"Failed to demonstrate GitHub service: {ex.Message}"
            };
        }
    }

    [McpServerTool(Name = "azsdk_example_ai_service"), Description("Demonstrates AI service integration using Azure OpenAI")]
    public async Task<ExampleServiceResponse> DemonstrateAIService(string userPrompt, CancellationToken ct = default)
    {
        var model = "gpt-4o";

        try
        {
            Dictionary<string, string> details = new()
            {
                ["model_used"] = model
            };

            // Get ChatClient from AzureOpenAIClient
            var chatClient = openAIClient.GetChatClient(model);

            var messages = new ChatMessage[]
            {
                new SystemChatMessage("You are a helpful assistant."),
                new UserChatMessage(userPrompt)
            };

            var response = await chatClient.CompleteChatAsync(messages, cancellationToken: ct);

            return new ExampleServiceResponse
            {
                ServiceName = "OpenAI",
                Operation = "Chat",
                Result = response.Value.Content[0].Text,
                Details = details
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error demonstrating AI service using model {Model} with prompt: {UserPrompt}", model, userPrompt);
            SetFailure();
            return new ExampleServiceResponse
            {
                ResponseError = $"Failed to demonstrate AI service: {ex.Message}"
            };
        }
    }

    [McpServerTool(Name = "azsdk_example_error_handling"), Description("Demonstrates error handling patterns in tools")]
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

    [McpServerTool(Name = "example_process_execution"), Description("Demonstrates running an external process (sleep) and capturing output")]
    public async Task<ExampleServiceResponse> DemonstrateProcessExecution(string time, CancellationToken ct = default)
    {
        try
        {
            // Trigger process timeout or normal sleep depending on whether value > 2
            var timespan = TimeSpan.FromSeconds(2);
            var process = processHelper.CreateForCrossPlatform("sleep", [time], "timeout", ["/t", time], Environment.CurrentDirectory);
            var result = await process.RunProcess(timespan, ct);
            var trimmed = (result.Output ?? string.Empty).Trim();

            if (result.ExitCode != 0)
            {
                SetFailure(result.ExitCode);
                return new ExampleServiceResponse
                {
                    ResponseErrors = [
                        $"Sleep example failed to run process",
                        result.Output
                    ]
                };
            }

            return new ExampleServiceResponse
            {
                ServiceName = "Process",
                Operation = "RunSleep",
                Result = trimmed,
                Details = new Dictionary<string, string>
                {
                    ["exit_code"] = result.ExitCode.ToString(),
                    ["raw_output"] = result.Output  ?? string.Empty
                }
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error demonstrating process execution for sleep: {time}", time);
            SetFailure();
            return new ExampleServiceResponse
            {
                ResponseError = $"Failed to execute process: {ex.Message}"
            };
        }
    }
}
#endif
