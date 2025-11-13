// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Tools;

public abstract class MCPNoCommandTool : MCPToolBase
{
    public override Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        CommandResponse response = new DefaultCommandResponse { ResponseError = "This tool does not support commands." };
        return Task.FromResult(response);
    }

    public override List<Command> GetCommandInstances()
    {
        return [];
    }
}
