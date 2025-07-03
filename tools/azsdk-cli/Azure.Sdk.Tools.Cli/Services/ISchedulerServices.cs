// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services
{
    /// <summary>
    /// Service interface for interacting with Dataverse tables used by the PowerApp scheduler
    /// </summary>
    public interface ISchedulerDataverseService
    {
        /// <summary>
        /// Creates a draft event record in Dataverse
        /// </summary>
        Task<string> CreateDraftEventAsync(SchedulerEvent schedulerEvent);
        
        /// <summary>
        /// Updates an existing event record in Dataverse
        /// </summary>
        Task<bool> UpdateEventAsync(string eventId, SchedulerEvent schedulerEvent);
        
        /// <summary>
        /// Gets an event record by ID
        /// </summary>
        Task<SchedulerEvent?> GetEventAsync(string eventId);
        
        /// <summary>
        /// Gets events by status
        /// </summary>
        Task<List<SchedulerEvent>> GetEventsByStatusAsync(string status);
        
        /// <summary>
        /// Updates event status
        /// </summary>
        Task<bool> UpdateEventStatusAsync(string eventId, string newStatus);
        
        /// <summary>
        /// Links calendar event ID to the Dataverse record
        /// </summary>
        Task<bool> LinkCalendarEventAsync(string eventId, string calendarEventId);
    }
    
    /// <summary>
    /// Service interface for managing shared calendar operations
    /// </summary>
    public interface ISharedCalendarService
    {
        /// <summary>
        /// Gets available time slots from shared calendar
        /// </summary>
        Task<List<AvailableTimeSlot>> GetAvailableTimeSlotsAsync(string calendarName, DateTime startDate, DateTime endDate);
        
        /// <summary>
        /// Creates a meeting in the shared calendar
        /// </summary>
        Task<string> CreateMeetingAsync(string calendarName, SchedulerEvent eventDetails);
        
        /// <summary>
        /// Checks if a specific time slot is available
        /// </summary>
        Task<bool> IsTimeSlotAvailableAsync(string calendarName, DateTime startTime, DateTime endTime);
        
        /// <summary>
        /// Gets calendar configuration for meeting type and environment
        /// </summary>
        MeetingCalendarConfig GetCalendarConfig(string meetingType, string environment);
        
        /// <summary>
        /// Creates a placeholder slot in shared calendar (for admin use)
        /// </summary>
        Task<string> CreatePlaceholderSlotAsync(string calendarName, DateTime startTime, DateTime endTime, string notes = "");
        
        /// <summary>
        /// Removes/blocks a specific meeting slot
        /// </summary>
        Task<bool> BlockMeetingSlotAsync(string calendarName, string placeholderEventId);
    }
}
