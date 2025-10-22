// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using Azure.AI.OpenAI;
using ModelContextProtocol.Server;
using OpenAI.Chat;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Microagents;

namespace Azure.Sdk.Tools.Cli.Tools.Example;

#if DEBUG
[McpServerToolType, Description("Example tool demonstrating various framework features and service integrations")]
public class ExampleTool(
    ILogger<ExampleTool> logger,
    IAzureService azureService,
    IDevOpsService devOpsService,
    IGitHubService gitHubService,
    IMicroagentHostService microagentHostService,
    IProcessHelper processHelper,
    IPowershellHelper powershellHelper,
    TokenUsageHelper tokenUsageHelper,
    AzureOpenAIClient openAIClient
) : MCPMultiCommandTool
{
    // Sub-command constants
    private const string AzureSubCommand = "azure";
    private const string DevOpsSubCommand = "devops";
    private const string GitHubSubCommand = "github";
    private const string AISubCommand = "ai";
    private const string ErrorSubCommand = "error";
    private const string ProcessSubCommand = "process";
    private const string PowershellSubCommand = "powershell";
    private const string MicroagentSubCommand = "microagent";

    // azsdk example demo <sub-command>
    public override CommandGroup[] CommandHierarchy { get; set; } = [
        SharedCommandGroups.Example,
        SharedCommandGroups.Demo
    ];

    // CLI Options and Arguments
    private readonly Argument<string> aiInputArg = new("chat-prompt")
    {
        Description = "Chat prompt surrounded with quotes",
        Arity = ArgumentArity.ExactlyOne
    };

    private readonly Argument<string> packageArgument = new("package")
    {
        Description = "Package name",
        Arity = ArgumentArity.ExactlyOne
    };

    private readonly Argument<string> errorInputArg = new("error-input")
    {
        Description = "Error type to simulate, can be argument, timeout, notfound, or any user input",
        Arity = ArgumentArity.ExactlyOne
    };

    private readonly Argument<string> processSleepArg = new("sleep")
    {
        Description = "How many seconds to sleep",
        Arity = ArgumentArity.ExactlyOne
    };

    private readonly Argument<string> powershellMessageArg = new("message")
    {
        Description = "Message to pass to the PowerShell script via parameter",
        Arity = ArgumentArity.ExactlyOne
    };

    private readonly Option<int> fibonacciIndexOption = new("--fibonacci")
    {
        Description = "Index (0-based) of Fibonacci number to compute using micro-agent",
        Required = true,
    };

    private readonly Option<string> tenantOption = new("--tenant", "-t")
    {
        Description = "Tenant ID",
        Required = false,
    };

    private readonly Option<string> languageOption = new("--language", "-l")
    {
        Description = "Programming language of the repository",
        Required = false,
    };

    private readonly Option<bool> forceFailureOption = new("--force-failure", "-f")
    {
        Description = "Force an error for demonstration",
        Required = false,
        DefaultValueFactory = _ => false,
    };

    protected override List<Command> GetCommands() =>
    [
        new(AzureSubCommand, "Demonstrate Azure service integration") { tenantOption },
        new(DevOpsSubCommand, "Demonstrate DevOps service integration") { packageArgument, languageOption },
        new(GitHubSubCommand, "Demonstrate GitHub service integration"),
        new(AISubCommand, "Demonstrate AI service integration") { aiInputArg },
        new(ErrorSubCommand, "Demonstrate error handling patterns") { errorInputArg, forceFailureOption },
        new(ProcessSubCommand, "Demonstrate spawning an external process (echo)") { processSleepArg },
        new(PowershellSubCommand, "Demonstrate PowerShell helper running a temp script with a parameter") { powershellMessageArg },
        new(MicroagentSubCommand, "Demonstrate micro-agent looping tool calls to compute Fibonacci") { fibonacciIndexOption }
    ];

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var commandName = parseResult.CommandResult.Command.Name;

        CommandResponse result = commandName switch
        {
            AzureSubCommand => await DemonstrateAzureService(parseResult.GetValue(tenantOption), ct),
            DevOpsSubCommand => await DemonstrateDevOpsService(parseResult.GetValue(packageArgument), parseResult.GetValue(languageOption), ct),
            GitHubSubCommand => await DemonstrateGitHubService(ct),
            AISubCommand => await DemonstrateAIService(parseResult.GetValue(aiInputArg), ct),
            ErrorSubCommand => await DemonstrateErrorHandling(parseResult.GetValue(errorInputArg), parseResult.GetValue(forceFailureOption), ct),
            ProcessSubCommand => await DemonstrateProcessExecution(parseResult.GetValue(processSleepArg), ct),
            PowershellSubCommand => await DemonstratePowershellExecution(parseResult.GetValue(powershellMessageArg), ct),
            MicroagentSubCommand => await DemonstrateMicroagentFibonacci(parseResult.GetValue(fibonacciIndexOption), ct),
            _ => new ExampleServiceResponse { ResponseError = $"Unknown command: {commandName}" }
        };

        return result;
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
            tokenUsageHelper.Add(model, response.Value.Usage.InputTokenCount, response.Value.Usage.OutputTokenCount);
            tokenUsageHelper.LogUsage();

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
            return new DefaultCommandResponse
            {
                ResponseError = $"Demonstrated error handling: {ex.GetType().Name}: {ex.Message}"
            };
        }
    }

    [McpServerTool(Name = "azsdk_example_process_execution"), Description("Demonstrates running an external process (sleep) and capturing output")]
    public async Task<ExampleServiceResponse> DemonstrateProcessExecution(string time, CancellationToken ct = default)
    {
        try
        {
            // Trigger process timeout or normal sleep depending on whether value > 2
            var options = new ProcessOptions(
                "sleep", [time],  // Run on unix
                "timeout", ["/t", time],  // Run on windows
                logOutputStream: true,
                timeout: TimeSpan.FromSeconds(2)
            );
            var result = await processHelper.Run(options, ct);
            var trimmed = (result.Output ?? string.Empty).Trim();

            if (result.ExitCode != 0)
            {
                return new ExampleServiceResponse
                {
                    ExitCode = result.ExitCode,
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
                    ["exit_code"] = result.ExitCode.ToString()
                }
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error demonstrating process execution for sleep: {time}", time);
            return new ExampleServiceResponse
            {
                ResponseError = $"Failed to execute process: {ex.Message}"
            };
        }
    }

    [McpServerTool(Name = "azsdk_example_powershell_execution"), Description("Demonstrates running a powershell script with a parameter")]
    public async Task<ExampleServiceResponse> DemonstratePowershellExecution(string message, CancellationToken ct = default)
    {
        string? tempFile = null;
        try
        {
            // Create a temporary PowerShell script that echoes a parameter via Write-Host
            var script = $"""
                param([string]$Message)
                Write-Host "1: $Message"
                Start-Sleep -Seconds 1
                Write-Host "2: $Message"
                Start-Sleep -Seconds 1
                Write-Error "Test error message, no failure"
                Start-Sleep -Seconds 1
                Write-Host "3: $Message"
            """;
            var guid = Guid.NewGuid().ToString()[..6];
            tempFile = Path.Combine(Path.GetTempPath(), $"azsdk_example_{guid}.ps1");
            await File.WriteAllTextAsync(tempFile, script, ct);

            var options = new PowershellOptions(tempFile, [message]);
            var result = await powershellHelper.Run(options, ct);
            var output = (result.Output ?? string.Empty).Trim();

            if (result.ExitCode != 0)
            {
                return new ExampleServiceResponse
                {
                    ServiceName = "PowerShell",
                    Operation = "RunTempScript",
                    ExitCode = result.ExitCode,
                    ResponseErrors = [
                        $"PowerShell script exited with code {result.ExitCode}",
                        result.Output ?? string.Empty
                    ]
                };
            }

            return new ExampleServiceResponse
            {
                ServiceName = "PowerShell",
                Operation = "RunTempScript",
                Result = output,
                Details = new Dictionary<string, string>
                {
                    ["script_path"] = tempFile,
                    ["exit_code"] = result.ExitCode.ToString()
                }
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error demonstrating PowerShell helper with message: {Message}", message);
            return new ExampleServiceResponse
            {
                ResponseError = $"Failed to run PowerShell script: {ex.Message}"
            };
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempFile))
            {
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch { /* ignore cleanup errors */ }
            }
        }
    }

    public record Fibonacci
    {
        public int Index { get; set; }
        public int Previous { get; set; }
        public int Current { get; set; }
    }

    [McpServerTool(Name = "azsdk_example_microagent_fibonacci"), Description("Demonstrates micro-agent computing Nth Fibonacci number via iterative tool calls")]
    public async Task<DefaultCommandResponse> DemonstrateMicroagentFibonacci(int n, CancellationToken ct = default)
    {
        try
        {
            if (n < 2)
            {
                return new DefaultCommandResponse { ResponseError = "--fibonacci must be >= 2 to run the micro-agent" };
            }

            var advanceTool = AgentTool<Fibonacci, Fibonacci>.FromFunc(
                name: "advance_state",
                description: "Advances state by one step",
                invokeHandler: (input, ct) =>
                {
                    return Task.FromResult(new Fibonacci
                    {
                        Index = input.Index + 1,
                        Previous = input.Current,
                        Current = input.Previous + input.Current
                    });
                });

            // Avoid mentioning 'fibonacci' in the instructions so the LLM doesn't try to calculate it directly
            var instructions = $"""
                Call advance_state repeatedly until the returned index == {n}.
                Return the '{nameof(Fibonacci.Current)}' value when index == {n}.
                Initial state is {nameof(Fibonacci.Index)}=1, {nameof(Fibonacci.Previous)}=0, {nameof(Fibonacci.Current)}=1.
            """;

            var agent = new Microagent<int>
            {
                Instructions = instructions,
                MaxToolCalls = 7,
                Tools = [advanceTool],
                ValidateResult = async result =>
                {
                    await Task.CompletedTask;

                    // Check the result for correctness using fibonacci formula.
                    var phi = (1 + Math.Sqrt(5)) / 2;
                    var psi = (1 - Math.Sqrt(5)) / 2;
                    var expected = (int)Math.Round((Math.Pow(phi, n) - Math.Pow(psi, n)) / Math.Sqrt(5));

                    // Example validation
                    if (result != expected)
                    {
                        // Failure reason will be provided to the LLM to self-correct
                        return new()
                        {
                            Success = false,
                            Reason = "Incorrect result, please try again"
                        };
                    }

                    return new() { Success = true };
                }
            };

            var resultValue = await microagentHostService.RunAgentToCompletion(agent, ct);

            tokenUsageHelper.LogUsage();
            return new DefaultCommandResponse { Result = $"Fibonacci({n}) = {resultValue}" };
        }
        catch (Exception ex)
        {
            tokenUsageHelper.LogUsage();
            logger.LogError(ex, "Error demonstrating micro-agent Fibonacci for n={n}", n);
            return new DefaultCommandResponse { ResponseError = $"Failed to compute Fibonacci({n}): {ex.Message}" };
        }
    }
}
#endif
