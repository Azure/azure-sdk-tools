// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Services;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Tools.EngSys;

[McpServerToolType, Description("Cleans up various engsys resources")]
public class CleanupTool : MCPTool
{
    public const string CleanupAgentsCommandName = "agents";
    private readonly IAzureAgentServiceFactory agentServiceFactory;
    private readonly ILogger<CleanupTool> logger;

    public Option<string> projectEndpointOpt = new("--project-endpoint", "-e")
    {
        Description = "The AI foundry project to clean up",
        Required = false,
    };

    public CleanupTool(
        IAzureAgentServiceFactory agentServiceFactory,
        ILogger<CleanupTool> logger
    ) : base()
    {
        this.agentServiceFactory = agentServiceFactory;
        this.logger = logger;

        CommandHierarchy =
        [
            SharedCommandGroups.EngSys, SharedCommandGroups.Cleanup  // azsdk eng cleanup
        ];
    }

    protected override Command GetCommand() => new(CleanupAgentsCommandName, "Cleanup ai agents") { projectEndpointOpt };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        if (parseResult.CommandResult.Command.Name != CleanupAgentsCommandName)
        {
            logger.LogError("Unknown command: {command}", parseResult.CommandResult.Command.Name);
            return new DefaultCommandResponse { ResponseError = $"Unknown command {parseResult.CommandResult.Command.Name}" };
        }
        var projectEndpoint = parseResult.GetValue(projectEndpointOpt);
        return await CleanupAgents(projectEndpoint, ct);
    }

    [McpServerTool(Name = "azsdk_cleanup_ai_agents"), Description("Clean up AI agents in an AI foundry project. Leave projectEndpoint empty if not specified")]
    public async Task<DefaultCommandResponse> CleanupAgents(string? projectEndpoint = null, CancellationToken ct = default)
    {
        try
        {
            var agentService = agentServiceFactory.Create(projectEndpoint, null);
            await agentService.DeleteAgents(ct);
            return new DefaultCommandResponse { };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while cleaning up agents in project '{ProjectName}'.", projectEndpoint ?? "unspecified");
            return new DefaultCommandResponse { ResponseError = ex.Message };
        }
    }
}
