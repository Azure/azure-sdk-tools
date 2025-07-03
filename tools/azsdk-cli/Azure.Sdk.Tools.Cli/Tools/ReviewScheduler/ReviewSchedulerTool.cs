using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Identity;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using ModelContextProtocol.Server;

[Description("This class contains MCP tools for scheduling and managing Azure SDK review meetings")]
[McpServerToolType]
public class ReviewSchedulerTool(
    IDevOpsService devopsService, 
    IOutputService output, 
    ILogger<ReviewSchedulerTool> logger,
    ISchedulerDataverseService? dataverseService = null,
    ISharedCalendarService? calendarService = null) : MCPTool
{
    private readonly Option<string> packageNameOpt = new(["--package-name"], "SDK package name") { IsRequired = true };
    private readonly Option<string> languageOpt = new(["--language"], "SDK language from one of the following ['.NET', 'Python', 'Java', 'JavaScript', 'Go']") { IsRequired = true };

    // Default configuration values matching the Power App
    private static readonly List<string> DefaultNotificationRecipients = new()
    {
        "t-remilne@microsoft.com"
        /*"azsdkexp@microsoft.com",
        "sdkowners@microsoft.com"*/
    };

    // Calendar configuration based on environment and meeting type
    private static readonly Dictionary<string, string> CalendarNames = new()
    {
        { "API_PROD", "API Meeting - PROD" },
        { "SDK_PROD", "SDK Meeting - PROD" }, 
        { "INFORMATIONAL_PROD", "Informational Meeting - PROD" },
        { "API_PPE", "API Meeting - PPE" },
        { "SDK_PPE", "SDK Meeting - PPE" },
        { "INFORMATIONAL_PPE", "Informational Meeting - PPE" },
        { "API_DEV", "API Meeting - Dev" },
        { "SDK_DEV", "SDK Meeting - Dev" },
        { "INFORMATIONAL_DEV", "Informational Meeting - Dev" }
    };
    
    private const string AzureSDKBotEmail = "azuresdk@microsoft.com"; // Bot account that owns shared calendars
    
    private const int DefaultMeetingDuration = 60; // minutes

    public override Command GetCommand()
    {
        var command = new Command("ReviewScheduler", "Schedules and manages Azure SDK review meetings.") { packageNameOpt, languageOpt };
        command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
        return command;
    }

    public async override Task HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        var packageName = ctx.ParseResult.GetValueForOption(packageNameOpt);
        var language = ctx.ParseResult.GetValueForOption(languageOpt);
        
        output.Output($"Review scheduler tool executed for package: {packageName}, language: {language}");
        await Task.CompletedTask;
    }

    /*
    Meeting variables
    string azureService, // Required
    string attendees = "",
    string productName = "",
    ReleasePlan? releasePlan = null,
    bool isFollowUpMeeting = false,
    string githubIssueLink = "",
    bool hasRestApiSpecsPR = false,
    string restApiSpecsPRPath = "",
    bool isPublicRepo = true,
    string heroScenariosLink = "",
    string coreConceptsLink = "",
    string description = ""
    */
    [McpServerTool(Name = "BookRESTAPIReviewMeeting"), Description("Books a review meeting for a specified SDK package.")]
    public async Task<string> BookRESTAPIReviewMeeting()
    {
        try
        {
            logger.LogInformation("Starting meeting booking process");
            
            // Step 1: Get available slots from shared calendar
            var environment = GetCurrentEnvironment(); // PROD, PPE, or DEV
            var meetingType = "API"; // Could be parameterized
            var config = calendarService?.GetCalendarConfig(meetingType, environment);
            var calendarName = config?.CalendarName ?? CalendarNames[$"API_{environment}"];
            
            if (calendarService != null)
            {
                var availableSlots = await calendarService.GetAvailableTimeSlotsAsync(calendarName, DateTime.Today, DateTime.Today.AddDays(30));
                
                if (!availableSlots.Any())
                {
                    return "No available meeting slots found. Please contact the admin to add more slots.";
                }
                
                // Step 2: Select the next available slot
                var selectedSlot = availableSlots.First();
                
                // Step 3: Create draft event in Dataverse (if service is available)
                string? eventId = null;
                if (dataverseService != null)
                {
                    var draftEvent = new SchedulerEvent
                    {
                        Title = "Azure SDK REST API Review",
                        Description = "Scheduled via MCP tool",
                        StartTime = selectedSlot.StartTime,
                        EndTime = selectedSlot.EndTime,
                        Status = "draft",
                        MeetingType = meetingType,
                        Environment = environment,
                        SharedCalendarName = calendarName
                    };
                    
                    eventId = await dataverseService.CreateDraftEventAsync(draftEvent);
                }
                
                // Step 4: Create actual meeting in shared calendar
                var schedulerEvent = new SchedulerEvent
                {
                    Id = eventId,
                    Title = "Azure SDK REST API Review",
                    Description = "Scheduled API review meeting",
                    StartTime = selectedSlot.StartTime,
                    EndTime = selectedSlot.EndTime,
                    MeetingType = meetingType,
                    Environment = environment
                };
                
                var meetingId = await calendarService.CreateMeetingAsync(calendarName, schedulerEvent);
                
                // Step 5: Link calendar event to Dataverse record
                if (dataverseService != null && eventId != null)
                {
                    await dataverseService.LinkCalendarEventAsync(eventId, meetingId);
                    await dataverseService.UpdateEventStatusAsync(eventId, "scheduled");
                }
                
                logger.LogInformation($"Successfully scheduled meeting with ID: {meetingId}");
                return $"Meeting scheduled successfully. Meeting ID: {meetingId}. Time: {selectedSlot.StartTime:yyyy-MM-dd HH:mm} UTC";
            }
            else
            {
                return await Task.FromResult("Fallback meeting scheduling not implemented");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error scheduling REST API review meeting");
            throw new InvalidOperationException("Failed to schedule meeting", ex);
        }
    }
    
    private async Task<bool> IsSlotAvailable(DateTime slotTime, string calendarName)
    {
        // Check if there's already a booked meeting at this time
        // This would involve checking both the placeholder calendar and actual bookings
        // Implementation depends on how the system tracks booked vs available slots
        return true;
    }
    
    private string GetCurrentEnvironment()
    {
        // Determine environment based on configuration or environment variables
        // This should be configurable based on your deployment
        return Environment.GetEnvironmentVariable("AZURE_SDK_ENVIRONMENT") ?? "DEV";
    }
}
