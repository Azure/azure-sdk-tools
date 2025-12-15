using Azure.Sdk.Tools.Cli.Microagents;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System.Text.Json;

namespace Azure.Sdk.Tools.Cli.Helpers;

public class ConversationLogger
{
    private readonly ILogger<ConversationLogger> logger;
    private readonly bool isEnabled;
    private readonly string logPath;

    public ConversationLogger(ILogger<ConversationLogger> logger)
    {
        this.logger = logger;
        this.isEnabled = logger.IsEnabled(LogLevel.Debug);
        
        if (isEnabled)
        {
            var timestamp = DateTime.UtcNow;
            var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), ".logs", timestamp.ToString("yyyyMMdd_HHmmss"));
            this.logPath = Path.Combine(logDirectory, "conversation.md");
        }
        else
        {
            this.logPath = string.Empty;
        }
    }

    public async Task InitializeAsync<TResult>(Microagent<TResult> agentDefinition) where TResult : notnull
    {
        if (!isEnabled)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            logger.LogInformation("Workspace conversation log: {workspaceLogPath}", logPath);
            
            var timestamp = DateTime.UtcNow;
            var logContent = $@"# 🤖 Microagent Conversation Log

**Started:** {timestamp:yyyy-MM-dd HH:mm:ss} UTC  
**Model:** `{agentDefinition.Model}`  
**Max Tool Calls:** {agentDefinition.MaxToolCalls}  
**Log File:** `{logPath}`

---

## 📋 System Prompt

```markdown
{agentDefinition.Instructions}
```

---

## 💬 Conversation Timeline

";
            await File.WriteAllTextAsync(logPath, logContent);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to initialize conversation log");
        }
    }

    public async Task LogConversationTurnAsync(int turnNumber, IList<ChatMessage> messages, string phase)
    {
        if (!isEnabled)
        {
            return;
        }

        try
        {
            var timestamp = DateTime.UtcNow;
            var turnContent = $@"
### 🔄 Turn {turnNumber} - {phase}
**Time:** {timestamp:HH:mm:ss} UTC

";

            // Log each message in the conversation
            foreach (var (msg, index) in messages.Select((m, i) => (m, i)))
            {
                var (role, emoji, content) = ExtractMessageInfo(msg);
                
                turnContent += $@"
#### {emoji} {role} Message {index + 1}

";
                
                if (msg is AssistantChatMessage assistant && assistant.ToolCalls?.Any() == true)
                {
                    // Handle tool calls separately
                    turnContent += $@"**Tool Calls:**
";
                    foreach (var toolCall in assistant.ToolCalls)
                    {
                        turnContent += $@"
- **🔧 {toolCall.FunctionName}**
  ```json
  {toolCall.FunctionArguments}
  ```
";
                    }
                }
                else if (!string.IsNullOrEmpty(content))
                {
                    // Regular text content
                    if (content.Length > 1000)
                    {
                        turnContent += $@"```
{content.Substring(0, 500)}

... [Content truncated - {content.Length} total characters] ...

{content.Substring(content.Length - 500)}
```

";
                    }
                    else
                    {
                        turnContent += $@"```
{content}
```

";
                    }
                }
            }

            turnContent += "\n---\n";

            // Write to workspace log file
            await File.AppendAllTextAsync(logPath, turnContent);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to log conversation turn {turnNumber}", turnNumber);
        }
    }

    public async Task LogToolResultAsync(string toolName, object? result, TimeSpan duration)
    {
        if (!isEnabled)
        {
            return;
        }

        try
        {
            var timestamp = DateTime.UtcNow;
            var toolContent = $@"
### 🔧 Tool Result: {toolName}
**Time:** {timestamp:HH:mm:ss} UTC | **Duration:** {duration.TotalMilliseconds:F0}ms

**Result:**
```json
{JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })}
```

---

";

            // Write to workspace log file
            await File.AppendAllTextAsync(logPath, toolContent);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to log tool result for {toolName}", toolName);
        }
    }

    public async Task LogConversationEndAsync(string status, string message)
    {
        if (!isEnabled)
        {
            return;
        }

        try
        {
            var timestamp = DateTime.UtcNow;
            var endContent = $@"
---

## 🏁 Conversation End

**Status:** {status}  
**Time:** {timestamp:yyyy-MM-dd HH:mm:ss} UTC  
**Message:** {message}

---

*Log generated by Azure SDK Tools CLI Microagent Host Service*
";
            await File.AppendAllTextAsync(logPath, endContent);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to log conversation end");
        }
    }

    private static (string role, string emoji, string content) ExtractMessageInfo(ChatMessage message)
    {
        return message switch
        {
            SystemChatMessage system => ("System", "🔧", system.Content.FirstOrDefault()?.Text ?? ""),
            UserChatMessage user => ("User", "👤", user.Content.FirstOrDefault()?.Text ?? ""),
            AssistantChatMessage assistant => ("Assistant", "🤖", assistant.Content.FirstOrDefault()?.Text ?? ""),
            ToolChatMessage toolMsg => ("Tool", "⚙️", toolMsg.Content.FirstOrDefault()?.Text ?? ""),
            _ => ("Unknown", "❓", "[Unknown message type]")
        };
    }
}