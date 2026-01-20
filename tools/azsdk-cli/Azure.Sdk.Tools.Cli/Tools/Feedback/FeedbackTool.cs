// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.Feedback;

/// <summary>
/// Tool for collecting user feedback and session evaluations.
/// Designed to be called at the end of agent sessions to gather:
/// - Agent-generated summary of the session
/// - User feedback and satisfaction
/// - User-reported issues or bugs
/// - Repository context
/// All data is sent to telemetry with user consent.
/// </summary>
[McpServerToolType, Description("Collects user feedback and session evaluation at the end of agent interactions")]
public class FeedbackTool(
    ILogger<FeedbackTool> logger
) : MCPTool
{
    // Command hierarchy: azsdk feedback submit
    public override CommandGroup[] CommandHierarchy { get; set; } = [
        SharedCommandGroups.Feedback
    ];

    // CLI Arguments and Options
    private readonly Argument<string> agentSummaryArg = new("agent-summary")
    {
        Description = "Agent-generated summary of what was accomplished in the session",
        Arity = ArgumentArity.ExactlyOne
    };

    private readonly Option<string> userFeedbackOption = new("--feedback", "-f")
    {
        Description = "User's feedback about the session (optional)",
        Required = false
    };

    private readonly Option<string[]> issuesOption = new("--issues", "-i")
    {
        Description = "User-reported issues or bugs encountered (optional, can be specified multiple times)",
        Required = false,
        AllowMultipleArgumentsPerToken = true
    };

    private readonly Option<string> sessionIdOption = new("--session-id", "-s")
    {
        Description = "Telemetry session ID (optional, will be generated if not provided)",
        Required = false
    };

    private readonly Option<string> repositoryOption = new("--repository", "-r")
    {
        Description = "Repository context (optional, will auto-detect from git if not provided)",
        Required = false
    };

    // CLI Command Configuration
    protected override Command GetCommand() => new("submit", "Submit user feedback and session evaluation")
    {
        agentSummaryArg,
        userFeedbackOption,
        issuesOption,
        sessionIdOption,
        repositoryOption
    };

    // CLI Command Handler
    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var agentSummary = parseResult.GetValue(agentSummaryArg);
        var userFeedback = parseResult.GetValue(userFeedbackOption);
        var issues = parseResult.GetValue(issuesOption);
        var sessionId = parseResult.GetValue(sessionIdOption);
        var repository = parseResult.GetValue(repositoryOption);

        return await CollectFeedback(
            agentSummary: agentSummary ?? string.Empty,
            userFeedback: userFeedback,
            issues: issues?.ToList(),
            sessionId: sessionId,
            repository: repository,
            ct: ct
        );
    }

    /// <summary>
    /// Collects user feedback and sends it to telemetry.
    /// This method should be called at the end of an agent session to:
    /// 1. Capture the agent's summary of what was accomplished
    /// 2. Request user feedback (optional - user can skip)
    /// 3. Collect any issues the user encountered
    /// 4. Send all data to telemetry with repository context
    /// 
    /// If the user provides no feedback or issues, no telemetry is tracked.
    /// This focuses on user-tracked bugs rather than agent-decided bugs.
    /// </summary>
    /// <param name="agentSummary">Agent's summary of the session tasks and outcomes</param>
    /// <param name="userFeedback">Optional user feedback about the session</param>
    /// <param name="issues">Optional list of user-reported issues or bugs</param>
    /// <param name="sessionId">Optional session ID (auto-generated if not provided)</param>
    /// <param name="repository">Optional repository context (auto-detected from git if not provided)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Feedback response indicating whether feedback was submitted</returns>
    [McpServerTool(Name = "azsdk_feedback_submit"), 
     Description("Submit user feedback and session evaluation. Call this at the end of agent sessions to collect user opinions, issues, and session summary. User can choose not to respond (no telemetry tracked). Focuses on user-reported issues rather than agent-decided bugs.")]
    public async Task<FeedbackResponse> CollectFeedback(
        string agentSummary,
        string? userFeedback = null,
        List<string>? issues = null,
        string? sessionId = null,
        string? repository = null,
        CancellationToken ct = default)
    {
        try
        {
            // If user provided no feedback and no issues, don't track telemetry
            var hasUserInput = !string.IsNullOrWhiteSpace(userFeedback) || (issues?.Count > 0);
            
            if (!hasUserInput)
            {
                logger.LogInformation("No user feedback or issues provided. Skipping telemetry tracking.");
                return new FeedbackResponse
                {
                    FeedbackSubmitted = false,
                    Message = "No feedback provided. Thank you for using the tool!"
                };
            }

            // Auto-detect repository if not provided
            if (string.IsNullOrWhiteSpace(repository))
            {
                try
                {
                    // Try to get repository name from current directory path
                    var currentDir = Environment.CurrentDirectory;
                    var dirParts = currentDir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    
                    // Look for common repository indicators in the path
                    var possibleRepoIndex = Array.FindLastIndex(dirParts, d => 
                        d.Contains("azure-sdk", StringComparison.OrdinalIgnoreCase) || 
                        d.Contains("repo", StringComparison.OrdinalIgnoreCase) ||
                        d.EndsWith("-tools", StringComparison.OrdinalIgnoreCase));
                    
                    if (possibleRepoIndex >= 0)
                    {
                        repository = dirParts[possibleRepoIndex];
                    }
                    else
                    {
                        repository = dirParts.Length > 0 ? dirParts[^1] : "Unknown";
                    }
                    
                    logger.LogInformation("Auto-detected repository from path: {repository}", repository);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not auto-detect repository information");
                    repository = "Unknown";
                }
            }

            // Generate session ID if not provided
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                sessionId = Guid.NewGuid().ToString();
                logger.LogInformation("Generated session ID: {sessionId}", sessionId);
            }

            // TODO: Send feedback to telemetry service
            // This would integrate with the existing telemetry infrastructure
            // For now, we'll log the feedback
            logger.LogInformation(
                "Feedback collected - SessionId: {sessionId}, Repository: {repository}, HasFeedback: {hasFeedback}, IssueCount: {issueCount}",
                sessionId, repository, !string.IsNullOrWhiteSpace(userFeedback), issues?.Count ?? 0
            );

            if (!string.IsNullOrWhiteSpace(userFeedback))
            {
                logger.LogInformation("User feedback: {feedback}", userFeedback);
            }

            if (issues?.Count > 0)
            {
                logger.LogInformation("User reported {count} issue(s):", issues.Count);
                foreach (var issue in issues)
                {
                    logger.LogInformation("  - {issue}", issue);
                }
            }

            logger.LogInformation("Agent summary: {summary}", agentSummary);

            return new FeedbackResponse
            {
                FeedbackSubmitted = true,
                SessionId = sessionId,
                AgentSummary = agentSummary,
                UserFeedback = userFeedback,
                IssuesReported = issues,
                Repository = repository,
                Message = "Thank you for your feedback! It has been recorded and will help improve the tool."
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error collecting feedback");
            return new FeedbackResponse
            {
                FeedbackSubmitted = false,
                ResponseError = $"Failed to collect feedback: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Requests feedback from the user in an interactive manner.
    /// This is a helper method for MCP clients that want to guide users through
    /// providing feedback step by step.
    /// </summary>
    /// <param name="agentSummary">Agent's summary to present to the user for confirmation</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Guidance on how to provide feedback</returns>
    [McpServerTool(Name = "azsdk_feedback_request"),
     Description("Request user feedback interactively. Shows the agent summary and prompts user for their feedback and any issues encountered. Returns guidance for collecting feedback.")]
    public async Task<FeedbackResponse> RequestFeedback(
        string agentSummary,
        CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Requesting user feedback for session");

            var message = $"""
                Session Summary:
                {agentSummary}

                Please provide your feedback:
                1. How did this session go? (Optional - you can skip if you prefer not to respond)
                2. Did you encounter any issues or bugs? (Optional)

                To submit feedback, use the 'azsdk_feedback_submit' tool with:
                - Your feedback (optional)
                - Any issues you encountered (optional)
                
                If you choose not to respond, no telemetry will be tracked.
                """;

            return await Task.FromResult(new FeedbackResponse
            {
                FeedbackSubmitted = false,
                AgentSummary = agentSummary,
                Message = message
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error requesting feedback");
            return new FeedbackResponse
            {
                FeedbackSubmitted = false,
                ResponseError = $"Failed to request feedback: {ex.Message}"
            };
        }
    }
}
