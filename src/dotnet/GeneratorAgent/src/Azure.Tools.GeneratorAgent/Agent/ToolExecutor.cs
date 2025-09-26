using Azure.Tools.GeneratorAgent.Tools;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Azure.Tools.GeneratorAgent.Agent;

/// <summary>
/// Tool executor that handles tool calls without complex agent interactions
/// </summary>
internal class ToolExecutor
{
    private readonly ITypeSpecToolHandler ToolHandler;
    private readonly ILogger<ToolExecutor> Logger;

    public ToolExecutor(ITypeSpecToolHandler toolHandler, ILogger<ToolExecutor> logger)
    {
        ToolHandler = toolHandler ?? throw new ArgumentNullException(nameof(toolHandler));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes a tool call by name with JSON arguments
    /// </summary>
    public async Task<string> ExecuteToolCallAsync(string toolName, string argumentsJson, CancellationToken cancellationToken = default)
    {
        try
        {
            return toolName switch
            {
                "list_typespec_files" => await ExecuteListTypeSpecFilesAsync(argumentsJson, cancellationToken),
                "get_typespec_file" => await ExecuteGetTypeSpecFileAsync(argumentsJson, cancellationToken),
                _ => CreateErrorResponse($"Unknown tool: {toolName}")
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error executing tool {ToolName}", toolName);
            return CreateErrorResponse($"Tool execution failed: {ex.Message}");
        }
    }

    private async Task<string> ExecuteListTypeSpecFilesAsync(string argumentsJson, CancellationToken cancellationToken)
    {
        // This tool doesn't need arguments
        var result = await ToolHandler.ListTypeSpecFilesAsync(cancellationToken);
        return JsonSerializer.Serialize(result);
    }

    private async Task<string> ExecuteGetTypeSpecFileAsync(string argumentsJson, CancellationToken cancellationToken)
    {
        // Parse arguments to get the filename
        using var args = JsonSerializer.Deserialize<JsonDocument>(argumentsJson);
        if (args?.RootElement.TryGetProperty("path", out var pathElement) == true)
        {
            var filename = pathElement.GetString();
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentException("Missing or empty 'path' in arguments");
            }
            
            var result = await ToolHandler.GetTypeSpecFileAsync(filename, cancellationToken);
            return JsonSerializer.Serialize(result);
        }
        
        throw new ArgumentException("Missing 'path' property in arguments");
    }

    private static string CreateErrorResponse(string errorMessage)
    {
        var errorResponse = new { error = errorMessage };
        return JsonSerializer.Serialize(errorResponse);
    }

}