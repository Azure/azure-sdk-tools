// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Telemetry;
using static Azure.Sdk.Tools.Cli.Telemetry.TelemetryConstants;

namespace Azure.Sdk.Tools.Cli.Tools.Core;

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

        using var activity = await telemetryService.StartActivity(ActivityName.CommandExecuted);

        try
        {
            var fullCommandName = string.Join('.', command.Parents.Reverse().Select(p => p.Name).Append(command.Name));
            var commandLine = string.Join(" ", parseResult.Tokens.Select(t => t.Value));
            activity?.SetTag(TagName.CommandName, fullCommandName);
            activity?.SetTag(TagName.CommandArgs, commandLine);

            CommandResponse response = await HandleCommand(parseResult, cancellationToken);
            // Pass response.GetType() otherwise we will only serialize CommandResponse base type properties
            activity?.SetTag(TagName.CommandResponse, JsonSerializer.Serialize(response, response.GetType()));
            activity?.SetStatus(response.ExitCode == 0 ? ActivityStatusCode.Ok : ActivityStatusCode.Error);

            AddCustomTelemetryFromResponse(activity, response);

            output.OutputCommandResponse(response);

            return response.ExitCode;
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            // Must be called before we manually flush below
            activity?.Dispose();
            // Force upload of events since we won't have background batching in CLI mode
            var flushed = telemetryService.Flush();
            if (flushed == false && debug)
            {
                output.OutputError("Telemetry flush did not complete before timeout");
            }
        }
    }

    // Add all properties to the activity's custom bag so we can filter them for well
    // known fields to be added to the telemetry event in TelemetryProcessor
    private static void AddCustomTelemetryFromResponse(Activity? activity, CommandResponse response)
    {
        if (activity == null)
        {
            return;
        }

        var responseProperties = response.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in responseProperties)
        {
            var value = prop.GetValue(response);
            var jsonAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
            string propertyName = jsonAttr?.Name ?? prop.Name;
            if (value != null)
            {
                // Avoid string/enum properties appearing like "\"Succeeded\"" from JSON serialization
                string serialized = value switch
                {
                    string s => s,
                    Enum e => e.ToString(),
                    _ => JsonSerializer.Serialize(value)
                };
                activity.SetCustomProperty(propertyName, serialized);
            }
        }
    }

    public abstract List<Command> GetCommandInstances();

    public abstract Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct);
}
