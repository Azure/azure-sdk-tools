using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Tools;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Azure.Tools.GeneratorAgent.Agent;

/// <summary>
/// Tool executor that handles tool calls without complex agent interactions
/// </summary>
internal class ToolExecutor
{
    private readonly Func<ValidationContext, ITypeSpecToolHandler> ToolHandlerFactory;
    private readonly AppSettings AppSettings;
    private readonly ILogger<ToolExecutor> Logger;
    private ValidationContext? _validationContext;

    public ToolExecutor(Func<ValidationContext, ITypeSpecToolHandler> toolHandlerFactory, AppSettings appSettings, ILogger<ToolExecutor> logger)
    {
        ToolHandlerFactory = toolHandlerFactory ?? throw new ArgumentNullException(nameof(toolHandlerFactory));
        AppSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Sets the validation context for this tool executor
    /// </summary>
    public void SetValidationContext(ValidationContext validationContext)
    {
        _validationContext = validationContext ?? throw new ArgumentNullException(nameof(validationContext));
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
                var name when name == AppSettings.ListTypeSpecFilesTool => await ExecuteListTypeSpecFilesAsync(argumentsJson, cancellationToken),
                var name when name == AppSettings.GetTypeSpecFileTool => await ExecuteGetTypeSpecFileAsync(argumentsJson, cancellationToken),
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
        if (_validationContext == null)
        {
            throw new InvalidOperationException("ValidationContext not set. Call SetValidationContext() first.");
        }

        // This tool doesn't need arguments
        var toolHandler = ToolHandlerFactory(_validationContext);
        var result = await toolHandler.ListTypeSpecFilesAsync(cancellationToken);
        return JsonSerializer.Serialize(result);
    }

    private async Task<string> ExecuteGetTypeSpecFileAsync(string argumentsJson, CancellationToken cancellationToken)
    {
        if (_validationContext == null)
        {
            throw new InvalidOperationException("ValidationContext not set. Call SetValidationContext() first.");
        }

        // Parse arguments to get the filename
        using var args = JsonSerializer.Deserialize<JsonDocument>(argumentsJson);
        if (args?.RootElement.TryGetProperty("path", out var pathElement) == true)
        {
            var filename = pathElement.GetString();
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentException("Missing or empty 'path' in arguments");
            }
            
            var toolHandler = ToolHandlerFactory(_validationContext);
            var result = await toolHandler.GetTypeSpecFileAsync(filename, cancellationToken);
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