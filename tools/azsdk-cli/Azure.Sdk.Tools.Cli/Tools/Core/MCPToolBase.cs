// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Attributes;
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
            activity?.AddTag(TagName.CommandName, fullCommandName);
            activity?.SetTag(TagName.CommandArgs, commandLine);

            CommandResponse response = await HandleCommand(parseResult, cancellationToken);
            activity?.SetTag(TagName.CommandResponse, output.Format(response));

            if (response.ExitCode == 0)
            {
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            else
            {
                activity?.SetStatus(ActivityStatusCode.Error);
            }

            // Hoist all properties from the output response object to the tag level.
            // This will enable KQL queries against command-specific output values
            // without having to parse the raw string output given to the user.
            if (activity != null)
            {
                AddCustomTelemetryFromResponse(activity, response);
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
        finally
        {
            // Force upload of events since we won't have background batching in CLI mode
            telemetryService.Flush();
        }
    }

    private static void AddCustomTelemetryFromResponse(Activity activity, CommandResponse response)
    {
        var responseProperties = response.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in responseProperties)
        {
            // Check if property is tagged for telemetry
            var telemetryAttr = prop.GetCustomAttributes<TelemetryAttribute>();
            if (telemetryAttr != null)
            {
                var value = prop.GetValue(response);
                var jsonAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
                string propertyName = jsonAttr?.Name ?? prop.Name;
                if (value != null)
                {
                    activity.AddTag(propertyName, JsonSerializer.Serialize(value));
                }
            }
        }
    }

    public abstract List<Command> GetCommandInstances();

    public abstract Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct);
}
