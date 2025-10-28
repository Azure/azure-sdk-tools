using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Constants;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Azure.Tools.GeneratorAgent.Agent;

/// <summary>
/// Tool executor that handles tool calls without complex agent interactions
/// </summary>
internal class ToolExecutor
{
    private readonly TypeSpecToolHandler ToolHandler;

    public ToolExecutor(TypeSpecToolHandler toolHandler)
    {
        ToolHandler = toolHandler ?? throw new ArgumentNullException(nameof(toolHandler));
    }

    /// <summary>
    /// Executes a tool call by name with JSON arguments
    /// </summary>
    /// <param name="toolName">The name of the tool to execute</param>
    /// <param name="argumentsJson">JSON arguments for the tool</param>
    /// <param name="validationContext">The validation context containing operation-specific data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<string> ExecuteToolCallAsync(string toolName, string argumentsJson, ValidationContext validationContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        if (string.IsNullOrWhiteSpace(toolName))
        {
            return CreateErrorResponse("Tool name cannot be null or empty");
        }
        
        try
        {
            return toolName switch
            {
                ToolNames.ListTypeSpecFiles => await ExecuteListTypeSpecFilesAsync(validationContext, cancellationToken),
                ToolNames.GetTypeSpecFile => await ExecuteGetTypeSpecFileAsync(validationContext, argumentsJson, cancellationToken),
                _ => CreateErrorResponse($"Unknown tool: {toolName}")
            };
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Tool execution failed: {ex.Message}");
        }
    }

    private async Task<string> ExecuteListTypeSpecFilesAsync(ValidationContext validationContext, CancellationToken cancellationToken)
    {
        var result = await ToolHandler.ListTypeSpecFilesAsync(validationContext, cancellationToken);
        return JsonSerializer.Serialize(result);
    }

    private async Task<string> ExecuteGetTypeSpecFileAsync(ValidationContext validationContext, string argumentsJson, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(argumentsJson))
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

                var result = await ToolHandler.GetTypeSpecFileAsync(filename, validationContext, cancellationToken);
                return JsonSerializer.Serialize(result);
            }

            throw new ArgumentException("Missing 'path' property in arguments");
        }
        else
        {
            return "{}";
        }
    }

    private static string CreateErrorResponse(string errorMessage)
    {
        var errorResponse = new { error = errorMessage };
        return JsonSerializer.Serialize(errorResponse);
    }

}