using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GitHub.Copilot.SDK;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure
{
    public class SessionConfigHelper
    {
        public static void ConfigureAgentActivityLogging(CopilotSession session)
        {
            // Configure agent activity logging here
            session.On(evt =>
            {
                switch (evt)
                {
                    case AssistantMessageDeltaEvent delta:
                        // Streaming message chunk - print incrementally
                        Console.Write(delta.Data.DeltaContent);
                        break;
                    case AssistantReasoningDeltaEvent reasoningDelta:
                        // Streaming reasoning chunk (if model supports reasoning)
                        Console.Write(reasoningDelta.Data.DeltaContent);
                        break;
                    case AssistantMessageEvent msg:
                        // Final message - complete content
                        Console.WriteLine("\n--- Final message ---");
                        Console.WriteLine(msg.Data.Content);
                        Console.WriteLine("\n---End of Final message ---");
                        break;
                    case AssistantReasoningEvent reasoningEvt:
                        // Final reasoning content (if model supports reasoning)
                        Console.WriteLine("--- Reasoning ---");
                        Console.WriteLine(reasoningEvt.Data.Content);
                        Console.WriteLine("--- End of Reasoning ---");
                        break;
                    case ToolExecutionStartEvent toolStart:
                        Console.WriteLine($"Tool execution started: {toolStart.Data.ToolName}, {toolStart.Data.Arguments?.ToString()}, {toolStart.Data.McpToolName}");
                        break;
                    case ToolExecutionCompleteEvent toolFinish:
                        Console.WriteLine($"Tool {toolFinish.Data.ToolCallId} execution finished: {toolFinish.Data.Result?.DetailedContent}");
                        break;
                    case SessionIdleEvent:
                        // Session finished processing
                        break;
                }
            });
        }
    }
}
