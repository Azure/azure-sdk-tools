# Azure SDK Tools - Copilot Instructions

## Azure Best Practices
**CRITICAL**: Before writing any Azure-related code, running Azure CLI commands, or performing Azure operations, you MUST:
1. Check if the `azure_development-get_best_practices` tool is available
2. If available, invoke it to get current Azure best practices
3. Apply those best practices to your implementation

This applies to ALL Azure-related work including:
- Writing Azure SDK code
- Running `az` commands in terminal
- Configuring Azure resources
- Working with Azure services (Storage, App Service, Functions, etc.)

## Task Completion and Feedback
**REQUIRED**: When you have fully completed the user's request:
1. Provide a brief summary of what was accomplished
2. **ALWAYS** invoke the `azsdk_feedback_request` tool with your summary
3. Wait for the user's response - they have three options:
   - **Provide feedback/issues** → Immediately call `azsdk_feedback_submit` with their input
   - **Explicitly decline/skip** → Call `azsdk_feedback_submit` with just the agentSummary (no telemetry tracked)
   - **Ignore the prompt** → No action needed, user can continue without responding
4. Only call `azsdk_feedback_submit` if the user actively responds (either with feedback or by declining)

**When to call the feedback request:**
- User's question has been answered completely
- Requested task/workflow is finished
- All subtasks are complete
- User indicates satisfaction or completion

**Example workflows:**

**Scenario 1 - User provides feedback:**
1. You complete a task and call `azsdk_feedback_request("Created new TypeSpec project and generated SDK")`
2. User responds: "Great work! But I had an issue with the build"
3. You immediately call `azsdk_feedback_submit("Created new TypeSpec project and generated SDK", "Great work!", ["Issue with the build"])`

**Scenario 2 - User declines:**
1. You call `azsdk_feedback_request("Fixed bug in pipeline")`
2. User responds: "No feedback" or "Skip"
3. You call `azsdk_feedback_submit("Fixed bug in pipeline")` with no feedback/issues

**Scenario 3 - User ignores:**
1. You call `azsdk_feedback_request("Updated documentation")`
2. User continues with a new request or doesn't respond
3. You do nothing - no need to call submit