// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Tools.Cli.Acp;
using Sdk.Tools.Cli.Mcp;
using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Models;
using Sdk.Tools.Cli.Services;
using Sdk.Tools.Cli.Tools.Package.Samples;

namespace Sdk.Tools.Cli;

public static class Program
{
    // Global option for OpenAI mode
    private static readonly Option<bool> UseOpenAiOption = new("--use-openai")
    {
        Description = "Use OpenAI-compatible API instead of GitHub Copilot. " +
                      "Requires OPENAI_API_KEY environment variable."
    };

    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("SDK CLI - Sample generation and SDK utilities")
        {
            UseOpenAiOption,
            BuildMcpCommand(),
            BuildAcpCommand(),
            BuildPackageCommand()
        };
        
        return await rootCommand.Parse(args).InvokeAsync();
    }
    
    private static Command BuildPackageCommand()
    {
        var sampleCommand = new Command("sample", "Sample-related commands")
        {
            BuildSampleGenerateCommand()
        };
        
        return new Command("package", "Package-related commands")
        {
            sampleCommand
        };
    }
    
    private static Command BuildMcpCommand()
    {
        var transportOption = new Option<string>("--transport") { Description = "Transport type (stdio, sse)", DefaultValueFactory = _ => "stdio" };
        var portOption = new Option<int>("--port") { Description = "Port for SSE transport", DefaultValueFactory = _ => 8080 };
        var logLevelOption = new Option<string>("--log-level") { Description = "Log level", DefaultValueFactory = _ => "info" };

        var command = new Command("mcp", "Start MCP server for AI agent integration (VS Code, Claude Desktop)")
        {
            transportOption,
            portOption,
            logLevelOption
        };
        
        command.SetAction(async (parseResult, ct) =>
        {
            var transport = parseResult.GetValue(transportOption)!;
            var port = parseResult.GetValue(portOption);
            var logLevel = parseResult.GetValue(logLevelOption)!;
            await McpServer.RunAsync(transport, port, logLevel);
        });
        
        return command;
    }
    
    private static Command BuildAcpCommand()
    {
        var logLevelOption = new Option<string>("--log-level") { Description = "Log level", DefaultValueFactory = _ => "info" };

        var command = new Command("acp", "Start ACP agent for interactive sample generation")
        {
            logLevelOption
        };
        
        command.SetAction(async (parseResult, ct) =>
        {
            var logLevel = parseResult.GetValue(logLevelOption)!;
            await SampleGeneratorAgentHost.RunAsync(logLevel);
        });
        
        return command;
    }
    
    private static IServiceProvider ConfigureServices(bool useOpenAi)
    {
        var services = new ServiceCollection();
        
        services.AddLogging(builder => builder.AddConsole());
        
        // Configure AI provider settings
        var aiSettings = AiProviderSettings.FromEnvironment();
        if (useOpenAi) aiSettings.UseOpenAi = true;
        services.AddSingleton(aiSettings);
        
        // Register AI debug logger
        services.AddSingleton<AiDebugLogger>();
        
        // Register AI service (supports both Copilot and OpenAI)
        services.AddSingleton<AiService>();
        services.AddSingleton<FileHelper>();
        services.AddSingleton<ConfigurationHelper>();
        services.AddSingleton<SampleGeneratorTool>();
        
        return services.BuildServiceProvider();
    }
    
    private static Command BuildSampleGenerateCommand()
    {
        var pathArg = new Argument<string>("path") { Description = "Path to SDK root directory" };
        var outputOption = new Option<string?>("--output") { Description = "Output directory for samples" };
        var languageOption = new Option<string?>("--language") { Description = "SDK language (dotnet, python, java, typescript, go)" };
        var promptOption = new Option<string?>("--prompt") { Description = "Custom generation prompt" };
        var countOption = new Option<int?>("--count") { Description = "Number of samples to generate (default: 5)" };
        var modelOption = new Option<string?>("--model") 
        { 
            Description = $"AI model (default: {AiProviderSettings.DefaultCopilotModel} for Copilot, {AiProviderSettings.DefaultOpenAiModel} for OpenAI)"
        };
        var dryRunOption = new Option<bool>("--dry-run") { Description = "Preview without writing files" };

        var command = new Command("generate", "Generate code samples for SDK package")
        {
            pathArg,
            outputOption,
            languageOption,
            promptOption,
            countOption,
            modelOption,
            dryRunOption
        };
        
        command.SetAction(async (parseResult, ct) =>
        {
            var path = parseResult.GetValue(pathArg)!;
            var output = parseResult.GetValue(outputOption);
            var language = parseResult.GetValue(languageOption);
            var prompt = parseResult.GetValue(promptOption);
            var count = parseResult.GetValue(countOption);
            var model = parseResult.GetValue(modelOption);
            var dryRun = parseResult.GetValue(dryRunOption);
            var useOpenAi = parseResult.GetValue(UseOpenAiOption);
            
            var services = ConfigureServices(useOpenAi);
            var tool = services.GetRequiredService<SampleGeneratorTool>();
            Environment.ExitCode = await tool.ExecuteAsync(path, output, language, prompt, count, model, dryRun, ct);
        });
        
        return command;
    }
}
