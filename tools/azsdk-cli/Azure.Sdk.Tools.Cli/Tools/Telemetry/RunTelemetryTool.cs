// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Collections.Concurrent;
using Azure.Sdk.Tools.Cli.Commands;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Tools.Core;
using Azure.Sdk.Tools.Cli.Telemetry;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Microagents;

namespace Azure.Sdk.Tools.Cli.Tools.Telemetry
{
    [McpServerToolType, Description("Session-based telemetry tool with upfront notification and end-of-session summarization")]
    public class RunTelemetryTool(
        ILogger<RunTelemetryTool> logger, 
        ITelemetryService telemetryService,
        IMicroagentHostService microagentHost) : MCPTool()
    {
        // MCP Tool Names
        private const string StartTelemetrySessionToolName = "azsdk_start_telemetry_session";
        private const string EndTelemetrySessionToolName = "azsdk_end_telemetry_session";
        private const string AddToTelemetrySessionToolName = "azsdk_add_to_telemetry_session";

        // Session storage - using pattern from test proxy
        private static readonly ConcurrentDictionary<string, TelemetrySession> ActiveSessions = new();

        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Telemetry];

        // Start session arguments
        private readonly Option<string> _userIdOpt = new("--user-id")
        {
            Description = "Optional user identifier for tracking",
            Required = false
        };

        private readonly Option<string> _contextOpt = new("--context")
        {
            Description = "Initial context about the conversation session",
            Required = false
        };

        private readonly Option<bool> _silentOpt = new("--silent")
        {
            Description = "Skip the upfront notification (not recommended)",
            Required = false,
            DefaultValueFactory = _ => false
        };

        // Session management arguments
        private readonly Argument<string> _sessionIdArg = new Argument<string>("session-id")
        {
            Description = "The session ID to operate on",
            Arity = ArgumentArity.ExactlyOne
        };

        private readonly Argument<string> _messageContentArg = new Argument<string>("message-content")
        {
            Description = "Message content to add to the session",
            Arity = ArgumentArity.ExactlyOne
        };

        // End session arguments
        private readonly Option<string> _finalContentOpt = new("--final-content")
        {
            Description = "Final conversation content to analyze",
            Required = false
        };

        protected override Command GetCommand()
        {
            var startCommand = new Command("start-session", "Start a new telemetry tracking session with user notification")
            {
                _userIdOpt,
                _contextOpt,
                _silentOpt
            };

            var addCommand = new Command("add-message", "Add a message to an active telemetry session")
            {
                _sessionIdArg,
                _messageContentArg
            };

            var endCommand = new Command("end-session", "End a telemetry session with AI summarization")
            {
                _sessionIdArg,
                _finalContentOpt
            };

            var mainCommand = new Command("telemetry", "Session-based conversation telemetry tracking");
            mainCommand.Add(startCommand);
            mainCommand.Add(addCommand);
            mainCommand.Add(endCommand);

            return mainCommand;
        }

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var commandName = parseResult.CommandResult.Command.Name;

            return commandName switch
            {
                "start-session" => await StartTelemetrySession(
                    parseResult.GetValue(_userIdOpt),
                    parseResult.GetValue(_contextOpt),
                    parseResult.GetValue(_silentOpt)),

                "add-message" => await AddToTelemetrySession(
                    parseResult.GetValue(_sessionIdArg) ?? "",
                    parseResult.GetValue(_messageContentArg) ?? ""),

                "end-session" => await EndTelemetrySession(
                    parseResult.GetValue(_sessionIdArg) ?? "",
                    parseResult.GetValue(_finalContentOpt)),

                _ => new DefaultCommandResponse 
                { 
                    ResponseError = $"Unknown command: '{commandName}'" 
                }
            };
        }

        [McpServerTool(Name = StartTelemetrySessionToolName), 
         Description("Starts a new telemetry session with upfront user notification. USAGE: Call this at the beginning of any conversation or task to notify the user about telemetry collection and start tracking. Always call this before using other telemetry tools. Returns a session ID for subsequent operations.")]
        public async Task<DefaultCommandResponse> StartTelemetrySession(
            string? userId = null,
            string? context = null,
            bool silent = false)
        {
            try
            {
                logger.LogInformation("Starting new telemetry session for user: {userId}", userId ?? "anonymous");

                // Create new session
                var session = new TelemetrySession(userId, context);
                
                if (!ActiveSessions.TryAdd(session.SessionId, session))
                {
                    return new DefaultCommandResponse
                    {
                        ResponseError = "**Error**: Failed to create telemetry session",
                        ExitCode = 1
                    };
                }

                // Start telemetry activity
                using var activity = await telemetryService.StartActivity("TelemetrySessionStarted");
                if (activity != null)
                {
                    activity.SetCustomProperty("session_id", session.SessionId);
                    activity.SetCustomProperty("session_start_time", session.StartTime.ToString("o"));
                    if (!string.IsNullOrEmpty(userId))
                    {
                        activity.SetCustomProperty("user_id", userId);
                    }
                    if (!string.IsNullOrEmpty(context))
                    {
                        activity.SetCustomProperty("initial_context", context);
                    }
                    activity.SetCustomProperty("operation_status", "success");
                }

                var responseBuilder = new System.Text.StringBuilder();
                
                if (!silent)
                {
                    responseBuilder.AppendLine("**TELEMETRY TRACKING NOTIFICATION**");
                    responseBuilder.AppendLine();
                    responseBuilder.AppendLine("**Your conversation session is now being tracked for analytics and improvement purposes.**");
                    responseBuilder.AppendLine();
                    responseBuilder.AppendLine("**What we collect:**");
                    responseBuilder.AppendLine("  • Conversation topics and themes");
                    responseBuilder.AppendLine("  • Usage patterns and feature interactions");
                    responseBuilder.AppendLine("  • Performance and error metrics");
                    responseBuilder.AppendLine("  • Session duration and engagement data");
                    responseBuilder.AppendLine();
                    responseBuilder.AppendLine("**Privacy protection:**");
                    responseBuilder.AppendLine("  • No personally identifiable information without consent");
                    responseBuilder.AppendLine("  • Data is aggregated and anonymized");
                    responseBuilder.AppendLine("  • Used only for service improvement");
                    responseBuilder.AppendLine("  • Complies with privacy policies");
                    responseBuilder.AppendLine();
                    responseBuilder.AppendLine("**Your control:**");
                    responseBuilder.AppendLine("  • Set `AZSDKTOOLS_COLLECT_TELEMETRY=false` to disable");
                    responseBuilder.AppendLine("  • Session will be summarized at conversation end");
                    responseBuilder.AppendLine();
                }

                responseBuilder.AppendLine($"**Session Started**: {session.SessionId}");
                responseBuilder.AppendLine($"**Started at**: {session.StartTime:yyyy-MM-dd HH:mm:ss UTC}");
                if (!string.IsNullOrEmpty(userId))
                {
                    responseBuilder.AppendLine($"**User**: {userId}");
                }
                if (!string.IsNullOrEmpty(context))
                {
                    responseBuilder.AppendLine($"**Context**: {context}");
                }

                logger.LogInformation("Telemetry session started: {sessionId}", session.SessionId);

                return new DefaultCommandResponse
                {
                    Message = responseBuilder.ToString(),
                    Duration = 1
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error starting telemetry session");
                return new DefaultCommandResponse
                {
                    ResponseError = $"**Error**: Failed to start telemetry session: {ex.Message}",
                    ExitCode = 1
                };
            }
        }

        [McpServerTool(Name = AddToTelemetrySessionToolName), 
         Description("Adds message content to an active telemetry session. USAGE: Call this periodically during conversations to capture user interactions, questions, and assistant responses. Use the session ID from start_telemetry_session. Helps build complete conversation context for AI analysis.")]
        public async Task<DefaultCommandResponse> AddToTelemetrySession(
            string sessionId,
            string messageContent)
        {
            try
            {
                if (!ActiveSessions.TryGetValue(sessionId, out var session))
                {
                    return new DefaultCommandResponse
                    {
                        ResponseError = $"**Error**: Session {sessionId} not found or expired",
                        ExitCode = 1
                    };
                }

                if (!session.IsActive)
                {
                    return new DefaultCommandResponse
                    {
                        ResponseError = $"**Error**: Session {sessionId} is no longer active",
                        ExitCode = 1
                    };
                }

                await session.AddMessage(messageContent);
                
                logger.LogInformation("Added message to session {sessionId}: {length} characters", 
                    sessionId, messageContent.Length);

                return new DefaultCommandResponse
                {
                    Message = $"**Message Added**: {messageContent.Length} characters added to session {sessionId}",
                    Duration = 1
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error adding message to telemetry session {sessionId}", sessionId);
                return new DefaultCommandResponse
                {
                    ResponseError = $"**Error**: Failed to add message to session: {ex.Message}",
                    ExitCode = 1
                };
            }
        }

        [McpServerTool(Name = EndTelemetrySessionToolName), 
         Description("Ends a telemetry session with AI-powered conversation summarization. USAGE: Call this when a conversation or task is complete to trigger AI analysis of the full conversation and save telemetry data. Always call this to properly close sessions started with start_telemetry_session. Provides conversation topic analysis and user feedback.")]
        public async Task<DefaultCommandResponse> EndTelemetrySession(
            string sessionId,
            string? finalContent = null)
        {
            try
            {
                if (!ActiveSessions.TryRemove(sessionId, out var session))
                {
                    return new DefaultCommandResponse
                    {
                        ResponseError = $"**Error**: Session {sessionId} not found",
                        ExitCode = 1
                    };
                }

                // Add final content if provided
                if (!string.IsNullOrEmpty(finalContent))
                {
                    await session.AddMessage(finalContent);
                }

                // End the session
                await session.EndSession();

                logger.LogInformation("Ending telemetry session: {sessionId}", sessionId);

                // Get full conversation content
                var conversationContent = await session.GetFullConversation();
                
                ConversationSummaryResult? analysisResult = null;
                
                // Perform AI analysis if there's content
                if (!string.IsNullOrWhiteSpace(conversationContent))
                {
                    analysisResult = await AnalyzeConversationWithAI(conversationContent);
                }

                // Start telemetry activity for session end
                using var activity = await telemetryService.StartActivity("TelemetrySessionCompleted");
                if (activity != null)
                {
                    activity.SetCustomProperty("session_id", sessionId);
                    activity.SetCustomProperty("session_duration_minutes", 
                        ((TimeSpan)session.Metadata["Duration"]).TotalMinutes.ToString("F2"));
                    activity.SetCustomProperty("message_count", session.Metadata["MessageCount"].ToString());
                    
                    if (analysisResult != null)
                    {
                        activity.SetCustomProperty("conversation_topic", analysisResult.Topic);
                        activity.SetCustomProperty("conversation_summary", analysisResult.Summary);
                        activity.SetCustomProperty("conversation_category", analysisResult.Category);
                        activity.SetCustomProperty("conversation_tags", string.Join(",", analysisResult.Tags));
                        activity.SetCustomProperty("ai_confidence_score", analysisResult.ConfidenceScore.ToString());
                        activity.SetCustomProperty("ai_analysis_completed", "true");
                    }
                    else
                    {
                        activity.SetCustomProperty("ai_analysis_completed", "false");
                    }
                    
                    if (!string.IsNullOrEmpty(session.UserId))
                    {
                        activity.SetCustomProperty("user_id", session.UserId);
                    }
                    
                    activity.SetCustomProperty("operation_status", "success");
                }

                // Build response
                var responseBuilder = new System.Text.StringBuilder();
                responseBuilder.AppendLine("**TELEMETRY SESSION COMPLETED**");
                responseBuilder.AppendLine();
                responseBuilder.AppendLine($"**Session ID**: {sessionId}");
                responseBuilder.AppendLine($"**Duration**: {((TimeSpan)session.Metadata["Duration"]).TotalMinutes:F1} minutes");
                responseBuilder.AppendLine($"**Messages**: {session.Metadata["MessageCount"]}");
                responseBuilder.AppendLine();

                if (analysisResult != null)
                {
                    responseBuilder.AppendLine("**AI CONVERSATION ANALYSIS**");
                    responseBuilder.AppendLine($"**Topic**: {analysisResult.Topic}");
                    responseBuilder.AppendLine($"**Summary**: {analysisResult.Summary}");
                    responseBuilder.AppendLine($"**Category**: {analysisResult.Category}");
                    responseBuilder.AppendLine($"**Tags**: {string.Join(", ", analysisResult.Tags)}");
                    responseBuilder.AppendLine($"**AI Confidence**: {analysisResult.ConfidenceScore}%");
                    responseBuilder.AppendLine();
                }

                responseBuilder.AppendLine("**Telemetry data has been collected and will be used to improve our services.**");
                responseBuilder.AppendLine("Thank you for contributing to better user experiences!");

                logger.LogInformation("Telemetry session completed: {sessionId} with topic: {topic}", 
                    sessionId, analysisResult?.Topic ?? "No topic generated");

                // Cleanup session resources
                session.Dispose();

                return new DefaultCommandResponse
                {
                    Message = responseBuilder.ToString(),
                    Duration = 1
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error ending telemetry session {sessionId}", sessionId);
                return new DefaultCommandResponse
                {
                    ResponseError = $"**Error**: Failed to end telemetry session: {ex.Message}",
                    ExitCode = 1
                };
            }
        }

        private async Task<ConversationSummaryResult?> AnalyzeConversationWithAI(string conversationContent)
        {
            try
            {
                var promptTemplate = new ConversationAnalysisPromptTemplate();
                
                var prompt = promptTemplate.BuildPrompt()
                    .Replace("{{conversation_content}}", conversationContent);

                var microagent = new Microagent<ConversationSummaryResult>
                {
                    Instructions = prompt
                };

                var result = await microagentHost.RunAgentToCompletion(microagent);
                
                if (result != null)
                {
                    logger.LogInformation("AI analysis completed with confidence: {confidence}", result.ConfidenceScore);
                    return result;
                }
                
                logger.LogWarning("AI analysis returned null result");
                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during AI conversation analysis");
                return null;
            }
        }
    }
}