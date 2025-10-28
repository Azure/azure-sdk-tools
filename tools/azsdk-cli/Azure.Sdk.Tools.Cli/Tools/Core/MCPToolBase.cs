// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Telemetry;
using static Azure.Sdk.Tools.Cli.Telemetry.TelemetryConstants;

namespace Azure.Sdk.Tools.Cli.Tools;

/// <summary>
/// This is the base class defining how an MCP enabled tool will interface with the server.
///
/// This covers:
///     - route registration/disambiguation
///     - compilation trim avoidance for reflection-included MCP tools
/// </summary>
public abstract class MCPToolBase
{
    private bool initialized = false;
    private bool debug = false;
    private IOutputHelper output { get; set; }
    private ITelemetryService telemetryService { get; set; }

    public virtual CommandGroup[] CommandHierarchy { get; set; } = [];

    public void Initialize(IOutputHelper outputHelper, ITelemetryService telemetryService, bool debug = false)
    {
        this.debug = debug;
        this.output = outputHelper;
        this.telemetryService = telemetryService;

        this.initialized = true;
    }

    public async Task<int> InstrumentedCommandHandler(Command command, ParseResult parseResult, CancellationToken cancellationToken)
    {
        if (!initialized)
        {
            throw new InvalidOperationException("Tool must be initialized with Initialize() before use");
        }

        using var tracer = TelemetryService.RegisterCliTelemetry(debug);

        // TODO: add client info
        using var activity = await telemetryService.StartActivity(ActivityName.CommandExecuted);
        Activity.Current = activity;

        try
        {
            var fullCommandName = string.Join('.', command.Parents.Reverse().Select(p => p.Name).Append(command.Name));
            var commandLine = string.Join(" ", parseResult.Tokens.Select(t => t.Value));
            activity?.AddTag(TagName.CommandName, fullCommandName);
            activity?.SetTag(TagName.CommandArgs, commandLine);

            CommandResponse response = await HandleCommand(parseResult, cancellationToken);
            var result = output.Format(response);

            activity?.SetTag(TagName.CommandResponse, result);

            if (response.ExitCode == 0)
            {
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            else
            {
                activity?.SetStatus(ActivityStatusCode.Error);
            }

            output.OutputCommandResponse(response);

            return response.ExitCode;
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public abstract List<Command> GetCommandInstances();

    public abstract Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct);
}
