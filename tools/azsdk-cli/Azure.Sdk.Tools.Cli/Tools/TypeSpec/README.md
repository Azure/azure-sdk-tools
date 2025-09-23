# azsdk ai-completion

The `ai-completion` command connects to the QA bot `/completion` endpoint.

It can be access via the CLI or as an MCP tool.

## Configuration

2 settings are required for both the CLI or MCP tool to work:

- AI_COMPLETION_ENDPOINT
- AI_COMPLETION_API_KEY

The `AI_COMPLETION_API_KEY` should not be required if running the `/completion` endpoint locally.

These are currently settable as environment variables.

## Building

Follow the [project readme](../../../README.md) to learn how to build and run this project.

## Configuring MCP

For vscode, create the `.vscode/mcp.json` file and add the development build of this package.

```json
{
  "servers": {
    "azsdk-w-qa-bot": {
      "type": "stdio",
      "command": "/path/to/repo/azure-sdk-tools/tsp-qa-bot/artifacts/bin/Azure.Sdk.Tools.Cli/Debug/net8.0/azsdk",
      "args": ["start"],
      "env": {
        "AI_COMPLETION_ENDPOINT": "https://completion.endpoint",
        "AI_COMPLETION_BOT_CLIENT_ID": "bot-service-client-id"
      }
    }
  }
}
```

## Testing in vscode

So far, I've found that you have to tell copilot while in agent mode to use the tool. I usually start with something like:

> Use the azsdk qa bot to ...

This could be tuned if we decide to make this part of the azsdk cli.
