// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;

namespace Azure.Sdk.Tools.Cli.Models
{
    /// <summary>
    /// Represents an event record in Dataverse that integrates with the PowerApp scheduler
    /// </summary>
    public class SchedulerEvent
    {
        public string? Id { get; set; }
        
        [Required]
        public string Title { get; set; } = string.Empty;
        
        public string Description { get; set; } = string.Empty;
        
        [Required]
        public DateTime StartTime { get; set; }
        
        [Required]
        public DateTime EndTime { get; set; }
        
        [Required]
        public string Status { get; set; } = "draft"; // draft, scheduled, completed, cancelled
        
        [Required]
        public string MeetingType { get; set; } = "API"; // API, SDK, Informational
        
        [Required]
        public string Environment { get; set; } = "PROD"; // PROD, PPE, DEV
        
        public string AzureService { get; set; } = string.Empty;
        
        public string ProductName { get; set; } = string.Empty;
        
        public string AttendeeEmails { get; set; } = string.Empty;
        
        public string OrganizerEmail { get; set; } = string.Empty;
        
        public string CalendarEventId { get; set; } = string.Empty;
        
        public string SharedCalendarName { get; set; } = string.Empty;
        
        public string GitHubIssueLink { get; set; } = string.Empty;
        
        public string RestApiSpecsPRPath { get; set; } = string.Empty;
        
        public bool IsPublicRepo { get; set; } = true;
        
        public string HeroScenariosLink { get; set; } = string.Empty;
        
        public string CoreConceptsLink { get; set; } = string.Empty;
        
        public bool IsFollowUpMeeting { get; set; } = false;
        
        public int? ReleasePlanWorkItemId { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public string CreatedBy { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Configuration for meeting calendar mapping
    /// </summary>
    public class MeetingCalendarConfig
    {
        public string MeetingType { get; set; } = string.Empty;
        public string Environment { get; set; } = string.Empty;
        public string CalendarName { get; set; } = string.Empty;
        public string BotUserEmail { get; set; } = string.Empty;
        public int DefaultDurationMinutes { get; set; } = 60;
        public string TimeZone { get; set; } = "Pacific Standard Time";
    }
    
    /// <summary>
    /// Represents an available time slot from shared calendar
    /// </summary>
    public class AvailableTimeSlot
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string CalendarName { get; set; } = string.Empty;
        public string PlaceholderEventId { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public bool IsAvailable { get; set; } = true;
    }
}
