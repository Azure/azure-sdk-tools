// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;

namespace Azure.Sdk.Tools.Cli.Tools.Core;

public abstract class MCPTool : MCPToolBase
{
    protected abstract Command GetCommand();

    public override List<Command> GetCommandInstances()
    {
        var command = GetCommand();
        command.SetAction((parseResult, cancellationToken) => InstrumentedCommandHandler(command, parseResult, cancellationToken));
        return [command];
    }
}
