<!-- This file provides repository-level instructions for GitHub Copilot Chat.
     It is automatically loaded when users interact with Copilot in this repo
     (VS Code, GitHub.com, etc.) to guide responses for TypeSpec authoring,
     SDK generation, API reviews, and other repo-specific workflows.

     For GitHub Copilot Code Review (the feature that posts inline PR comments),
     see copilot-review-instructions.md in this same directory.
     Docs: https://docs.github.com/en/copilot/concepts/agents/code-review -->

# When to invoke the azure-typespec-author skill

The `azure-typespec-author` skill **must** be invoked immediately in all modes (including plan mode) for any task that involves creating and modifying TypeSpec (`.tsp`) files except for `client.tsp` under the specification directory in this repository. This includes but is not limited to:

- Adding, bumping, or promoting API versions (preview, stable)
- Adding or modifying resources, operations, models, properties, or decorators
- Changing visibility, constraints, breaking changes, LRO patterns, or suppressions
- Defining or updating operationId, spread models, or extension resources
- Converting Swagger to TypeSpec (post-conversion edits)

**If you are unsure whether a user request involves TypeSpec authoring, ask the user to confirm before proceeding.** For example, if the request mentions API changes, versioning, resource definitions, or spec modifications but does not explicitly mention TypeSpec, prompt the user:

> "This request may involve TypeSpec specification changes. Would you like me to use the azure-typespec-author skill to help with this?"

If the user confirms, invoke the `azure-typespec-author` skill immediately. Do **not** build typespec authoring related plan or attempt to make `.tsp` file changes without invoking this skill first.

**Do NOT use this skill for:** SDK generation, releasing SDK packages, `client.tsp` or code customization, or standalone MCP tool calls that do not involve editing `.tsp` files.