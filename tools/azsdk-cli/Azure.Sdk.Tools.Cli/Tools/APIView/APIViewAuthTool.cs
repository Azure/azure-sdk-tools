using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.APIView;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Services;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.APIView;

[McpServerToolType]
[Description("APIView authentication operations including status checks and guidance")]
public class APIViewAuthTool : MCPMultiCommandTool
{
    public override CommandGroup[] CommandHierarchy { get; set; } = [
        SharedCommandGroups.APIView,
        SharedCommandGroups.APIViewAuth
    ];

    private readonly IAPIViewService _apiViewService;
    private readonly ILogger<APIViewAuthTool> _logger;

    private readonly Option<string> environmentOption = new("--environment",
        description: "The APIView environment (defaults to production)", getDefaultValue: () => "production");

    public APIViewAuthTool(ILogger<APIViewAuthTool> logger, IAPIViewService apiViewService)
    {
        _logger = logger;
        _apiViewService = apiViewService;
    }

    protected override List<Command> GetCommands()
    {
        var authCheckCmd = new Command("check", "Check APIView authentication status")
        {
            environmentOption
        };

        var authGuidanceCmd = new Command("guidance", "Get APIView authentication guidance");

        return [authCheckCmd, authGuidanceCmd];
    }

    public override async Task<CommandResponse> HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        string commandName = ctx.ParseResult.CommandResult.Command.Name;
        APIViewResponse result = commandName switch
        {
            "check" => await CheckAuthentication(ctx.ParseResult.GetValueForOption(environmentOption)),
            "guidance" => await GetAuthenticationGuidance(),
            _ => new APIViewResponse { ResponseError = $"Unknown auth command: {commandName}" }
        };

        return result;
    }

    [McpServerTool(Name = "azsdk_apiview_check_authentication")]
    [Description("Check APIView authentication status and available credentials")]
    public async Task<APIViewResponse> CheckAuthentication(string? environment = null)
    {
        try
        {
            AuthenticationStatus result = await _apiViewService.CheckAuthenticationStatusAsync(environment);
            string serializedResult = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            _logger.LogInformation("Authentication status retrieved successfully. Status: {Status}", serializedResult);
            return new APIViewResponse
            {
                Success = true,
                Message = "Authentication status retrieved successfully",
                Data = serializedResult
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check authentication status");
            return new APIViewResponse { ResponseError = $"Failed to check authentication: {ex.Message}" };
        }
    }

    [McpServerTool(Name = "azsdk_apiview_get_authentication_guidance")]
    [Description("Get detailed guidance on how to authenticate with APIView")]
    public async Task<APIViewResponse> GetAuthenticationGuidance()
    {
        try
        {
            AuthenticationGuidance result = await _apiViewService.GetAuthenticationGuidanceAsync();
            string serializedResult = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            _logger.LogInformation("Authentication guidance retrieved successfully. Guidance: {Guidance}", serializedResult);
            return new APIViewResponse
            {
                Success = true,
                Message = "Authentication guidance retrieved successfully",
                Data = serializedResult
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get authentication guidance");
            return new APIViewResponse { ResponseError = $"Failed to get authentication guidance: {ex.Message}" };
        }
    }
}
