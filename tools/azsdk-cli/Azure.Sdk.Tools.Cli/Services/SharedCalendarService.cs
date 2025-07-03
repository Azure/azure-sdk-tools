// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Identity;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace Azure.Sdk.Tools.Cli.Services
{
    /// <summary>
    /// Implementation of shared calendar service for Azure SDK meeting scheduling
    /// </summary>
    public class SharedCalendarService : ISharedCalendarService
    {
        private readonly GraphServiceClient _graphClient;
        private readonly ILogger<SharedCalendarService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _botUserEmail;
        
        private static readonly Dictionary<string, MeetingCalendarConfig> CalendarConfigs = new()
        {
            { "API_PROD", new MeetingCalendarConfig { MeetingType = "API", Environment = "PROD", CalendarName = "API Meeting - PROD", BotUserEmail = "azuresdk@microsoft.com" } },
            { "SDK_PROD", new MeetingCalendarConfig { MeetingType = "SDK", Environment = "PROD", CalendarName = "SDK Meeting - PROD", BotUserEmail = "azuresdk@microsoft.com" } },
            { "INFORMATIONAL_PROD", new MeetingCalendarConfig { MeetingType = "Informational", Environment = "PROD", CalendarName = "Informational Meeting - PROD", BotUserEmail = "azuresdk@microsoft.com" } },
            { "API_PPE", new MeetingCalendarConfig { MeetingType = "API", Environment = "PPE", CalendarName = "API Meeting - PPE", BotUserEmail = "azuresdk@microsoft.com" } },
            { "SDK_PPE", new MeetingCalendarConfig { MeetingType = "SDK", Environment = "PPE", CalendarName = "SDK Meeting - PPE", BotUserEmail = "azuresdk@microsoft.com" } },
            { "INFORMATIONAL_PPE", new MeetingCalendarConfig { MeetingType = "Informational", Environment = "PPE", CalendarName = "Informational Meeting - PPE", BotUserEmail = "azuresdk@microsoft.com" } },
            { "API_DEV", new MeetingCalendarConfig { MeetingType = "API", Environment = "DEV", CalendarName = "API Meeting - Dev", BotUserEmail = "azuresdk@microsoft.com" } },
            { "SDK_DEV", new MeetingCalendarConfig { MeetingType = "SDK", Environment = "DEV", CalendarName = "SDK Meeting - Dev", BotUserEmail = "azuresdk@microsoft.com" } },
            { "INFORMATIONAL_DEV", new MeetingCalendarConfig { MeetingType = "Informational", Environment = "DEV", CalendarName = "Informational Meeting - Dev", BotUserEmail = "azuresdk@microsoft.com" } }
        };
        
        public SharedCalendarService(IConfiguration configuration, ILogger<SharedCalendarService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _botUserEmail = configuration["AzureSDKScheduler:BotUserEmail"] ?? "azuresdk@microsoft.com";
            
            var credential = new DefaultAzureCredential();
            _graphClient = new GraphServiceClient(credential);
        }
        
        public async Task<List<AvailableTimeSlot>> GetAvailableTimeSlotsAsync(string calendarName, DateTime startDate, DateTime endDate)
        {
            try
            {
                _logger.LogInformation($"Getting available time slots for calendar: {calendarName}");
                
                var availableSlots = new List<AvailableTimeSlot>();
                
                // Get placeholder events from the bot user's calendar
                var calendarView = await _graphClient.Users[_botUserEmail].CalendarView
                    .GetAsync(requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.StartDateTime = startDate.ToString("yyyy-MM-ddTHH:mm:ss.fffK");
                        requestConfiguration.QueryParameters.EndDateTime = endDate.ToString("yyyy-MM-ddTHH:mm:ss.fffK");
                        requestConfiguration.QueryParameters.Filter = $"categories/any(c:c eq '{calendarName}')";
                    });
                
                if (calendarView?.Value != null)
                {
                    foreach (var evt in calendarView.Value)
                    {
                        if (evt.Start?.DateTime != null && evt.End?.DateTime != null &&
                            DateTime.TryParse(evt.Start.DateTime, out var startTime) &&
                            DateTime.TryParse(evt.End.DateTime, out var endTime))
                        {
                            // Check if this slot is still available (not already booked)
                            var isAvailable = await IsTimeSlotAvailableAsync(calendarName, startTime, endTime);
                            
                            availableSlots.Add(new AvailableTimeSlot
                            {
                                StartTime = startTime,
                                EndTime = endTime,
                                CalendarName = calendarName,
                                PlaceholderEventId = evt.Id ?? "",
                                Notes = evt.Subject ?? "",
                                IsAvailable = isAvailable
                            });
                        }
                    }
                }
                
                _logger.LogInformation($"Found {availableSlots.Count} time slots for {calendarName}");
                return availableSlots.Where(s => s.IsAvailable).OrderBy(s => s.StartTime).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get available time slots for calendar: {calendarName}");
                return new List<AvailableTimeSlot>();
            }
        }
        
        public async Task<string> CreateMeetingAsync(string calendarName, SchedulerEvent eventDetails)
        {
            try
            {
                _logger.LogInformation($"Creating meeting in calendar: {calendarName}");
                
                var newEvent = new Event
                {
                    Subject = eventDetails.Title,
                    Body = new ItemBody
                    {
                        ContentType = BodyType.Html,
                        Content = eventDetails.Description
                    },
                    Start = new DateTimeTimeZone
                    {
                        DateTime = eventDetails.StartTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                        TimeZone = "UTC"
                    },
                    End = new DateTimeTimeZone
                    {
                        DateTime = eventDetails.EndTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                        TimeZone = "UTC"
                    },
                    IsOnlineMeeting = true,
                    OnlineMeetingProvider = OnlineMeetingProviderType.TeamsForBusiness,
                    Categories = new List<string> { calendarName, "Azure SDK Review", "Scheduled Meeting" }
                };
                
                // Add attendees if provided
                if (!string.IsNullOrEmpty(eventDetails.AttendeeEmails))
                {
                    var attendeeEmails = eventDetails.AttendeeEmails.Split(',', ';')
                        .Select(email => email.Trim())
                        .Where(email => !string.IsNullOrEmpty(email));
                    
                    newEvent.Attendees = attendeeEmails.Select(email => new Attendee
                    {
                        EmailAddress = new EmailAddress
                        {
                            Address = email,
                            Name = email
                        },
                        Type = AttendeeType.Required
                    }).ToList();
                }
                
                // Create the meeting in the bot user's calendar
                var createdEvent = await _graphClient.Users[_botUserEmail].Events.PostAsync(newEvent);
                
                _logger.LogInformation($"Created meeting with ID: {createdEvent?.Id}");
                return createdEvent?.Id ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to create meeting in calendar: {calendarName}");
                throw;
            }
        }
        
        public async Task<bool> IsTimeSlotAvailableAsync(string calendarName, DateTime startTime, DateTime endTime)
        {
            try
            {
                // Check if there are any existing meetings (non-placeholder) at this time
                var calendarView = await _graphClient.Users[_botUserEmail].CalendarView
                    .GetAsync(requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.StartDateTime = startTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK");
                        requestConfiguration.QueryParameters.EndDateTime = endTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK");
                        requestConfiguration.QueryParameters.Filter = $"categories/any(c:c eq 'Scheduled Meeting')";
                    });
                
                // If there are any scheduled meetings at this time, the slot is not available
                return calendarView?.Value?.Count == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check time slot availability");
                return false;
            }
        }
        
        public MeetingCalendarConfig GetCalendarConfig(string meetingType, string environment)
        {
            var key = $"{meetingType.ToUpper()}_{environment.ToUpper()}";
            return CalendarConfigs.TryGetValue(key, out var config) ? config : CalendarConfigs["API_PROD"];
        }
        
        public async Task<string> CreatePlaceholderSlotAsync(string calendarName, DateTime startTime, DateTime endTime, string notes = "")
        {
            try
            {
                _logger.LogInformation($"Creating placeholder slot in calendar: {calendarName}");
                
                var placeholderEvent = new Event
                {
                    Subject = notes,
                    Start = new DateTimeTimeZone
                    {
                        DateTime = startTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                        TimeZone = "Pacific Standard Time"
                    },
                    End = new DateTimeTimeZone
                    {
                        DateTime = endTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                        TimeZone = "Pacific Standard Time"
                    },
                    ShowAs = FreeBusyStatus.Free, // Placeholder events show as free
                    Categories = new List<string> { calendarName, "Placeholder Slot" }
                };
                
                var createdEvent = await _graphClient.Users[_botUserEmail].Events.PostAsync(placeholderEvent);
                
                _logger.LogInformation($"Created placeholder slot with ID: {createdEvent?.Id}");
                return createdEvent?.Id ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to create placeholder slot in calendar: {calendarName}");
                throw;
            }
        }
        
        public async Task<bool> BlockMeetingSlotAsync(string calendarName, string placeholderEventId)
        {
            try
            {
                _logger.LogInformation($"Blocking meeting slot: {placeholderEventId}");
                
                // Delete the placeholder event to block the slot
                await _graphClient.Users[_botUserEmail].Events[placeholderEventId].DeleteAsync();
                
                _logger.LogInformation($"Successfully blocked meeting slot: {placeholderEventId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to block meeting slot: {placeholderEventId}");
                return false;
            }
        }
    }
}
