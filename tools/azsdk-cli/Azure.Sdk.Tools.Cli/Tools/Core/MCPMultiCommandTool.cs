// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;

namespace Azure.Sdk.Tools.Cli.Tools;

public abstract class MCPMultiCommandTool : MCPToolBase
{
    protected abstract List<Command> GetCommands();

    public override List<Command> GetCommandInstances()
    {
        var commands = GetCommands();
        foreach (var cmd in commands)
        {
            cmd.SetAction((parseResult, cancellationToken) => InstrumentedCommandHandler(cmd, parseResult, cancellationToken));
        }
        return commands;
    }
}
