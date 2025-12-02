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
            SetHandlers(cmd);
        }
        return commands;
    }

    private void SetHandlers(Command command)
    {
        command.SetAction((parseResult, cancellationToken) => InstrumentedCommandHandler(command, parseResult, cancellationToken));
        foreach (var child in command.Subcommands.OfType<Command>())
        {
            SetHandlers(child);
        }
    }
}
