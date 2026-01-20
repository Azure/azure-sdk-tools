# Feedback Tool

The Feedback Tool collects user feedback and session evaluations at the end of agent interactions. It's designed to gather user-reported issues and feedback rather than relying on agent-decided bugs, making the feedback more reliable and actionable.

## Features

- **User-driven feedback**: Only tracks telemetry when the user provides input
- **Session summaries**: Collects agent-generated summaries with user approval
- **Issue tracking**: Captures user-reported issues and bugs
- **Repository context**: Auto-detects or accepts repository information
- **Telemetry integration**: Sends feedback data to the telemetry system

## Usage

### CLI Command

```bash
# Basic usage - no feedback (won't track telemetry)
azsdk feedback submit "Agent completed the task"

# With user feedback
azsdk feedback submit "Created a new tool" --feedback "Great job!"

# With issues reported
azsdk feedback submit "Fixed bugs" --issues "Performance lag" "UI glitch"

# Complete example with all options
azsdk feedback submit "Completed 3 tasks successfully" \
  --feedback "Excellent work!" \
  --issues "Minor lag" "UI issue" \
  --repository "azure-sdk-tools" \
  --session-id "custom-session-123"

# JSON output
azsdk feedback submit "Task done" --feedback "Good" --output json
```

### MCP Server Usage

The tool exposes two MCP methods for LLM agents:

#### 1. `azsdk_feedback_request`

Request feedback from the user interactively. Shows the agent summary and prompts for feedback.

**Parameters:**
- `agentSummary` (string): Agent's summary of what was accomplished

**Example:**
```
#azsdk_feedback_request I completed setting up the TypeSpec project and generated the SDK code
```

#### 2. `azsdk_feedback_submit`

Submit user feedback and session evaluation.

**Parameters:**
- `agentSummary` (string, required): Agent's summary of the session
- `userFeedback` (string, optional): User's feedback about the session
- `issues` (array of strings, optional): User-reported issues or bugs
- `sessionId` (string, optional): Session ID (auto-generated if not provided)
- `repository` (string, optional): Repository name (auto-detected if not provided)

**Example:**
```
#azsdk_feedback_submit agentSummary:"Created feedback tool" userFeedback:"This is helpful" issues:["No issues"]
```

## Behavior

### No Telemetry Without User Input

If the user provides **no feedback** and **no issues**, the tool will:
- Not track any telemetry
- Return a "Feedback not submitted" response
- Thank the user for using the tool

This ensures that telemetry is only collected when users actively choose to provide input.

### Auto-Detection

The tool automatically:
- **Generates a session ID** if not provided
- **Detects repository name** from the current directory path if not provided

### Response Fields

The tool returns a `FeedbackResponse` object with:
- `feedbackSubmitted` (bool): Whether feedback was actually submitted
- `sessionId` (string): The session identifier
- `agentSummary` (string): The agent's summary
- `userFeedback` (string): User's feedback
- `issuesReported` (array): List of user-reported issues
- `repository` (string): Repository context
- `message` (string): User-facing message
- `operationStatus`: Success or failure status

## Integration with Telemetry

When feedback is submitted (user provides feedback or issues), the data is:
1. Logged with structured information
2. Sent to the telemetry system (TODO: integration pending)
3. Associated with the session ID
4. Tagged with repository context

## Best Practices for Agents

1. **Call at session end**: Invoke the feedback tool when you believe you've completed the user's request
2. **Provide clear summaries**: Write concise, factual summaries of what was accomplished
3. **Respect user choice**: Don't require feedback - users can skip it
4. **Focus on user issues**: Capture what the *user* reports as problems, not what you think went wrong

## Development

### Files

- **Tool**: [Azure.Sdk.Tools.Cli/Tools/Feedback/FeedbackTool.cs](../Azure.Sdk.Tools.Cli/Tools/Feedback/FeedbackTool.cs)
- **Response**: [Azure.Sdk.Tools.Cli/Models/Responses/FeedbackResponse.cs](../Azure.Sdk.Tools.Cli/Models/Responses/FeedbackResponse.cs)
- **Tests**: [Azure.Sdk.Tools.Cli.Tests/Tools/Feedback/FeedbackToolTests.cs](../Azure.Sdk.Tools.Cli.Tests/Tools/Feedback/FeedbackToolTests.cs)

### Running Tests

```bash
cd tools/azsdk-cli
dotnet test --filter "FullyQualifiedName~FeedbackToolTests"
```

All tests should pass, confirming the tool's behavior.
