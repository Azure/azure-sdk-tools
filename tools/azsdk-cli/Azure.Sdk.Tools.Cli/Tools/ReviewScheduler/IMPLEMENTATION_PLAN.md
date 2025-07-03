# Azure SDK Meeting Scheduler MCP Implementation

## Overview
This document outlines the implementation plan for integrating the Azure SDK Meeting Scheduler with the existing PowerApp system using MCP (Model Context Protocol) server tools.

## Current State
- ✅ Basic MCP tool structure implemented
- ✅ Microsoft Graph integration for calendar events
- ✅ Models created for Dataverse integration
- ✅ Service interfaces defined
- ⚠️ Missing actual Dataverse service implementation
- ⚠️ Missing shared calendar integration

## Next Steps for Implementation

### 1. Add Required NuGet Packages

Add these packages to `Azure.Sdk.Tools.Cli.csproj`:

```xml
<!-- For Dataverse integration -->
<PackageReference Include="Microsoft.PowerPlatform.Dataverse.Client" Version="1.1.32" />
<PackageReference Include="Microsoft.PowerPlatform.Dataverse.Client.Extensions" Version="1.1.32" />

<!-- For enhanced calendar operations -->
<PackageReference Include="Microsoft.Graph.Calendar" Version="5.79.0" />
```

### 2. Implement Dataverse Service

Create `SchedulerDataverseService.cs`:

```csharp
public class SchedulerDataverseService : ISchedulerDataverseService
{
    private readonly ServiceClient _serviceClient;
    
    public SchedulerDataverseService(string connectionString)
    {
        _serviceClient = new ServiceClient(connectionString);
    }
    
    // Implementation for all interface methods
    // - CreateDraftEventAsync
    // - UpdateEventAsync  
    // - GetEventAsync
    // - etc.
}
```

### 3. Implement Shared Calendar Service

Create `SharedCalendarService.cs`:

```csharp
public class SharedCalendarService : ISharedCalendarService
{
    private readonly GraphServiceClient _graphClient;
    private readonly Dictionary<string, MeetingCalendarConfig> _calendarConfigs;
    
    // Implementation for:
    // - GetAvailableTimeSlotsAsync
    // - CreateMeetingAsync
    // - IsTimeSlotAvailableAsync
    // - GetCalendarConfig
}
```

### 4. Environment Configuration

Add configuration in `appsettings.json`:

```json
{
  "AzureSDKScheduler": {
    "DataverseConnectionString": "AuthType=OAuth;...",
    "BotUserEmail": "azuresdk@microsoft.com",
    "Environments": {
      "PROD": {
        "ApiCalendar": "API Meeting - PROD",
        "SdkCalendar": "SDK Meeting - PROD",
        "InformationalCalendar": "Informational Meeting - PROD"
      },
      "PPE": {
        "ApiCalendar": "API Meeting - PPE",
        "SdkCalendar": "SDK Meeting - PPE", 
        "InformationalCalendar": "Informational Meeting - PPE"
      },
      "DEV": {
        "ApiCalendar": "API Meeting - Dev",
        "SdkCalendar": "SDK Meeting - Dev",
        "InformationalCalendar": "Informational Meeting - Dev"
      }
    }
  }
}
```

### 5. Register Services in DI Container

Update `Program.cs` or DI registration:

```csharp
services.AddSingleton<ISchedulerDataverseService>(provider =>
    new SchedulerDataverseService(configuration.GetConnectionString("Dataverse")));
    
services.AddSingleton<ISharedCalendarService, SharedCalendarService>();
```

### 6. Permission Requirements

Ensure the application has these permissions:

**Microsoft Graph API:**
- `Calendars.ReadWrite.Shared` - To access shared calendars
- `Mail.Send` - For notifications
- `User.Read` - Basic user info

**Dataverse:**
- Read/Write access to the Events table
- Connection to the appropriate Dataverse environment

### 7. Key Integration Points

#### A. Query Available Slots
```csharp
var slots = await calendarService.GetAvailableTimeSlotsAsync("API Meeting - PROD", DateTime.Today, DateTime.Today.AddDays(30));
```

#### B. Create Draft Event in Dataverse
```csharp
var draftEvent = new SchedulerEvent { Status = "draft", MeetingType = "API" };
var eventId = await dataverseService.CreateDraftEventAsync(draftEvent);
```

#### C. Schedule Meeting
```csharp
var meetingId = await calendarService.CreateMeetingAsync(calendarName, eventDetails);
await dataverseService.LinkCalendarEventAsync(eventId, meetingId);
```

### 8. PowerApp Integration

The scheduler tool can be called from PowerApps using the query parameter pattern:
- `https://schedulertool.com?event={eventId}` - Edit existing event
- Direct MCP calls for programmatic scheduling

### 9. Testing Strategy

1. **Unit Tests**: Test service implementations with mocked dependencies
2. **Integration Tests**: Test with actual Dataverse and Graph API
3. **PPE Testing**: Use PPE environment before production deployment

### 10. Deployment Considerations

- Environment-specific configuration
- Secure credential management (Key Vault)
- Monitor API rate limits for Graph calls
- Logging and telemetry for troubleshooting

## Architecture Diagram

```
PowerApp <-> Dataverse Events Table <-> MCP Server Tools
                                           |
                                    Microsoft Graph
                                           |
                                   Shared Calendars
                                    (azuresdk bot)
```

## Current File Status

- ✅ `ReviewSchedulerTool.cs` - Main MCP tool with placeholder implementations
- ✅ `SchedulerModels.cs` - Data models for events and configuration
- ✅ `ISchedulerServices.cs` - Service interfaces
- ❌ `SchedulerDataverseService.cs` - **Need to implement**
- ❌ `SharedCalendarService.cs` - **Need to implement**
- ❌ Configuration setup - **Need to implement**

## Immediate Next Actions

1. Add the required NuGet packages
2. Implement `SchedulerDataverseService` 
3. Implement `SharedCalendarService`
4. Add configuration and DI registration
5. Test with PPE environment
6. Deploy to production

This approach maintains compatibility with the existing PowerApp while providing programmatic access via MCP tools.
